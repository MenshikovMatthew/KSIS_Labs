using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace P2PChat
{
    // Типы сообщений для собственного протокола поверх TCP
    public enum MessageType : byte
    {
        Text = 1,   // обычное текстовое сообщение
        Name = 2    // передача имени при установке соединения
    }

    // Хранит информацию о подключённом узле (пире)
    public class PeerInfo
    {
        public IPAddress RemoteIP { get; set; }
        public string RemoteName { get; set; }
        public TcpClient TcpClient { get; set; }
        public NetworkStream Stream { get; set; }
        public Task ReadTask { get; set; }
    }

    // Главный класс, реализующий логику P2P-чата
    public class ChatNode
    {
        // Фиксированные порты
        private readonly int udpPort = 11000;   // для обнаружения (UDP)
        private readonly int tcpPort = 11001;   // для обмена сообщениями (TCP)

        private readonly IPAddress localIP;
        private readonly string localName;

        private UdpClient udpClient;
        private TcpListener tcpListener;

        // Потокобезопасный словарь активных пиров (ключ – IP-адрес)
        private readonly ConcurrentDictionary<string, PeerInfo> peers = new ConcurrentDictionary<string, PeerInfo>();

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly List<Task> backgroundTasks = new List<Task>();

        // Синхронизация при добавлении/удалении пиров (чтобы избежать двойного подключения)
        private readonly object peersLock = new object();

        public ChatNode(IPAddress localIP, string localName)
        {
            this.localIP = localIP;
            this.localName = localName;
        }

        // -------------------------------------------------------------------------
        // Публичные методы
        // -------------------------------------------------------------------------

        // Запускает узел: инициализирует сокеты, запускает фоновые задачи, оповещает о себе
        public async Task StartAsync()
        {
            LogEvent("Запуск узла", ConsoleColor.Green);

            // Инициализация UDP-сокета
            udpClient = new UdpClient(new IPEndPoint(localIP, udpPort));
            udpClient.EnableBroadcast = true;

            // Инициализация TCP-слушателя с проверкой занятости порта
            try
            {
                tcpListener = new TcpListener(localIP, tcpPort);
                tcpListener.Start();
            }
            catch (SocketException ex)
            {
                LogEvent($"Ошибка: порт {tcpPort} уже используется. Завершение работы.", ConsoleColor.Red);
                throw;
            }

            // Фоновые задачи
            backgroundTasks.Add(Task.Run(() => ReceiveUdpAsync(cts.Token)));
            backgroundTasks.Add(Task.Run(() => AcceptTcpClientsAsync(cts.Token)));

            // Оповещение о своём появлении
            await SendBroadcastAsync();

            // Основной цикл ввода
            await HandleUserInputAsync(cts.Token);
        }

        // Останавливает узел: отменяет задачи, закрывает сокеты
        public void Stop()
        {
            cts.Cancel();
            udpClient?.Close();
            tcpListener?.Stop();
            foreach (var peer in peers.Values)
            {
                peer.TcpClient?.Close();
            }
            peers.Clear();
            LogEvent("Узел остановлен", ConsoleColor.Red);
        }

        // -------------------------------------------------------------------------
        // Логирование
        // -------------------------------------------------------------------------

        // Выводит в консоль событие с меткой времени и цветом
        private void LogEvent(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");
            Console.ResetColor();
        }

        // Выводит в консоль сообщение (своё или чужое) с меткой времени
        private void LogMessage(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]{text}");
            Console.ResetColor();
        }

        // -------------------------------------------------------------------------
        // UDP (обнаружение узлов)
        // -------------------------------------------------------------------------

        // Отправляет широковещательный UDP-пакет со своим именем
        private async Task SendBroadcastAsync()
        {
            try
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(localName);
                IPEndPoint broadcastEp = new IPEndPoint(IPAddress.Broadcast, udpPort);
                await udpClient.SendAsync(nameBytes, nameBytes.Length, broadcastEp);
                LogEvent("Отправлен широковещательный UDP пакет", ConsoleColor.DarkGray);
            }
            catch (Exception ex)
            {
                LogEvent($"Ошибка отправки UDP broadcast: {ex.Message}", ConsoleColor.Red);
            }
        }

        // Принимает UDP-пакеты, при получении имени от неизвестного узла инициирует TCP-подключение
        private async Task ReceiveUdpAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // ReceiveAsync не принимает CancellationToken, поэтому используем Task.WhenAny
                    var receiveTask = udpClient.ReceiveAsync();
                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(-1, token));

                    if (completedTask == receiveTask)
                    {
                        UdpReceiveResult result = await receiveTask;
                        string remoteName = Encoding.UTF8.GetString(result.Buffer);
                        IPAddress remoteIP = result.RemoteEndPoint.Address;

                        // Игнорируем свой собственный пакет
                        if (remoteIP.Equals(localIP))
                            continue;

                        // Проверяем, что имя не пустое (базовая валидация)
                        if (string.IsNullOrWhiteSpace(remoteName))
                        {
                            LogEvent($"Получен UDP пакет от {remoteIP} с пустым именем, игнорируем", ConsoleColor.Yellow);
                            continue;
                        }

                        LogEvent($"Получен UDP пакет от {remoteIP} с именем '{remoteName}'", ConsoleColor.DarkGray);

                        // Если пир ещё не подключён, инициируем TCP-соединение
                        if (!peers.ContainsKey(remoteIP.ToString()))
                        {
                            Task.Run(() => ConnectToPeerAsync(remoteIP, token), token);
                        }
                    }
                    else
                    {
                        // Запрошена отмена
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { /* нормальное завершение */ }
            catch (Exception ex)
            {
                LogEvent($"Ошибка приёма UDP: {ex.Message}", ConsoleColor.Red);
            }
        }

        // -------------------------------------------------------------------------
        // TCP (установка и обслуживание соединений)
        // -------------------------------------------------------------------------

        // Принимает входящие TCP-соединения и передаёт их обработчику
        private async Task AcceptTcpClientsAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var acceptTask = tcpListener.AcceptTcpClientAsync();
                    var completedTask = await Task.WhenAny(acceptTask, Task.Delay(-1, token));

                    if (completedTask == acceptTask)
                    {
                        TcpClient client = await acceptTask;
                        Task.Run(() => HandleTcpClientAsync(client, token), token);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogEvent($"Ошибка приёма TCP: {ex.Message}", ConsoleColor.Red);
            }
        }

        // Обрабатывает входящее TCP-соединение: читает имя пира, отправляет своё имя, сохраняет пира
        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken token)
        {
            IPEndPoint remoteEp = (IPEndPoint)client.Client.RemoteEndPoint;
            IPAddress remoteIP = remoteEp.Address;
            NetworkStream stream = client.GetStream();

            try
            {
                // ---- Чтение имени удалённого узла ----
                byte[] typeBuf = new byte[1];
                int read = await ReadWithCancellationAsync(stream, typeBuf, 0, 1, token);
                if (read != 1 || (MessageType)typeBuf[0] != MessageType.Name)
                {
                    LogEvent($"Неверный протокол от {remoteIP} (ожидался тип Name)", ConsoleColor.Yellow);
                    client.Close();
                    return;
                }

                byte[] lenBuf = new byte[4];
                read = await ReadWithCancellationAsync(stream, lenBuf, 0, 4, token);
                if (read != 4)
                {
                    client.Close();
                    return;
                }
                int nameLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuf, 0));

                byte[] nameBuf = new byte[nameLen];
                read = await ReadWithCancellationAsync(stream, nameBuf, 0, nameLen, token);
                if (read != nameLen)
                {
                    client.Close();
                    return;
                }
                string remoteName = Encoding.UTF8.GetString(nameBuf);

                LogEvent($"Входящее TCP соединение от {remoteIP} с именем '{remoteName}'", ConsoleColor.DarkGray);

                // ---- Проверка на дубликат ----
                lock (peersLock)
                {
                    if (peers.ContainsKey(remoteIP.ToString()))
                    {
                        LogEvent($"Уже есть соединение с {remoteIP}, закрываем новое", ConsoleColor.Yellow);
                        client.Close();
                        return;
                    }
                }

                // ---- Отправляем своё имя ----
                await SendNameAsync(stream, token);

                // ---- Создаём запись о пире ----
                var peer = new PeerInfo
                {
                    RemoteIP = remoteIP,
                    RemoteName = remoteName,
                    TcpClient = client,
                    Stream = stream
                };

                lock (peersLock)
                {
                    peers[remoteIP.ToString()] = peer;
                }

                LogEvent($"Пользователь {remoteName} ({remoteIP}) подключился", ConsoleColor.Green);

                // ---- Запускаем чтение сообщений от этого пира ----
                peer.ReadTask = Task.Run(() => ReadFromPeerAsync(peer, token), token);
            }
            catch (Exception ex)
            {
                LogEvent($"Ошибка при обработке входящего TCP от {remoteIP}: {ex.Message}", ConsoleColor.Red);
                client.Close();
            }
        }

        // Инициирует исходящее TCP-подключение к узлу
        private async Task ConnectToPeerAsync(IPAddress remoteIP, CancellationToken token)
        {
            try
            {
                LogEvent($"Попытка подключиться к {remoteIP}...", ConsoleColor.DarkGray);
                var client = new TcpClient();

                try
                {
                    // Привязываем исходящий сокет к своему локальному IP, чтобы удалённый узел видел правильный адрес
                    client.Client.Bind(new IPEndPoint(localIP, 0));

                    await client.ConnectAsync(remoteIP.ToString(), tcpPort);
                    token.ThrowIfCancellationRequested();

                    NetworkStream stream = client.GetStream();

                    // Отправляем своё имя
                    await SendNameAsync(stream, token);

                    // Читаем имя пира
                    byte[] typeBuf = new byte[1];
                    int read = await ReadWithCancellationAsync(stream, typeBuf, 0, 1, token);
                    if (read != 1 || (MessageType)typeBuf[0] != MessageType.Name)
                    {
                        LogEvent($"Неверный протокол от {remoteIP} (ожидался тип Name)", ConsoleColor.Yellow);
                        client.Close();
                        return;
                    }

                    byte[] lenBuf = new byte[4];
                    read = await ReadWithCancellationAsync(stream, lenBuf, 0, 4, token);
                    if (read != 4) throw new Exception("Не удалось прочитать длину имени");
                    int nameLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuf, 0));

                    byte[] nameBuf = new byte[nameLen];
                    read = await ReadWithCancellationAsync(stream, nameBuf, 0, nameLen, token);
                    if (read != nameLen) throw new Exception("Не удалось прочитать имя");
                    string remoteName = Encoding.UTF8.GetString(nameBuf);

                    LogEvent($"Установлено исходящее соединение с {remoteIP} (имя '{remoteName}')", ConsoleColor.DarkGray);

                    // Проверка дубликата
                    lock (peersLock)
                    {
                        if (peers.ContainsKey(remoteIP.ToString()))
                        {
                            LogEvent($"Уже есть соединение с {remoteIP}, закрываем новое", ConsoleColor.Yellow);
                            client.Close();
                            return;
                        }
                    }

                    var peer = new PeerInfo
                    {
                        RemoteIP = remoteIP,
                        RemoteName = remoteName,
                        TcpClient = client,
                        Stream = stream
                    };

                    lock (peersLock)
                    {
                        peers[remoteIP.ToString()] = peer;
                    }

                    LogEvent($"Пользователь {remoteName} ({remoteIP}) подключился", ConsoleColor.Green);

                    peer.ReadTask = Task.Run(() => ReadFromPeerAsync(peer, token), token);
                }
                catch
                {
                    client.Close();
                    throw;
                }
            }
            catch (Exception ex)
            {
                LogEvent($"Ошибка подключения к {remoteIP}: {ex.Message}", ConsoleColor.Red);
            }
        }

        // Вспомогательный метод: чтение с возможностью отмены
        private async Task<int> ReadWithCancellationAsync(NetworkStream stream, byte[] buffer, int offset, int size, CancellationToken token)
        {
            var readTask = stream.ReadAsync(buffer, offset, size);
            var completedTask = await Task.WhenAny(readTask, Task.Delay(-1, token));

            if (completedTask == readTask)
            {
                return await readTask;
            }
            else
            {
                token.ThrowIfCancellationRequested();
                return 0; // не достигается
            }
        }

        // Отправляет своё имя по TCP
        private async Task SendNameAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(localName);
            byte[] type = new byte[] { (byte)MessageType.Name };
            byte[] len = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(nameBytes.Length));

            await stream.WriteAsync(type, 0, 1, token);
            await stream.WriteAsync(len, 0, 4, token);
            await stream.WriteAsync(nameBytes, 0, nameBytes.Length, token);
        }

        // Постоянно читает сообщения от конкретного пира и выводит их
        private async Task ReadFromPeerAsync(PeerInfo peer, CancellationToken token)
        {
            try
            {
                NetworkStream stream = peer.Stream;
                while (!token.IsCancellationRequested)
                {
                    byte[] typeBuf = new byte[1];
                    int read = await ReadWithCancellationAsync(stream, typeBuf, 0, 1, token);
                    if (read == 0) break; // соединение закрыто

                    MessageType msgType = (MessageType)typeBuf[0];
                    if (msgType != MessageType.Text) // игнорируем не текстовые
                        continue;

                    byte[] lenBuf = new byte[4];
                    read = await ReadWithCancellationAsync(stream, lenBuf, 0, 4, token);
                    if (read != 4) break;
                    int msgLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuf, 0));

                    byte[] msgBuf = new byte[msgLen];
                    read = await ReadWithCancellationAsync(stream, msgBuf, 0, msgLen, token);
                    if (read != msgLen) break;

                    string message = Encoding.UTF8.GetString(msgBuf);
                    LogMessage($" {peer.RemoteName} ({peer.RemoteIP}): {message}", ConsoleColor.Cyan);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogEvent($"Ошибка чтения от {peer.RemoteIP}: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                RemovePeer(peer);
            }
        }

        // Удаляет пира из списка и закрывает соединение
        private void RemovePeer(PeerInfo peer)
        {
            lock (peersLock)
            {
                if (peers.TryRemove(peer.RemoteIP.ToString(), out _))
                {
                    LogEvent($"Пользователь {peer.RemoteName} ({peer.RemoteIP}) отключился", ConsoleColor.Yellow);
                }
            }
            peer.TcpClient?.Close();
        }

        // -------------------------------------------------------------------------
        // Отправка сообщений и пользовательский ввод
        // -------------------------------------------------------------------------

        // Рассылает текстовое сообщение всем подключённым пирам
        private async Task SendToAllPeersAsync(string message)
        {
            byte[] msgBytes = Encoding.UTF8.GetBytes(message);
            byte[] type = new byte[] { (byte)MessageType.Text };
            byte[] len = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(msgBytes.Length));

            List<Task> sendTasks = new List<Task>();

            foreach (var peer in peers.Values)
            {
                try
                {
                    NetworkStream stream = peer.Stream;
                    // Отправляем асинхронно, не оборачивая в Task.Run
                    sendTasks.Add(stream.WriteAsync(type, 0, 1)
                        .ContinueWith(_ => stream.WriteAsync(len, 0, 4))
                        .Unwrap()
                        .ContinueWith(_ => stream.WriteAsync(msgBytes, 0, msgBytes.Length))
                        .Unwrap());
                }
                catch (Exception ex)
                {
                    LogEvent($"Ошибка отправки {peer.RemoteIP}: {ex.Message}", ConsoleColor.Red);
                    RemovePeer(peer);
                }
            }

            try
            {
                await Task.WhenAll(sendTasks);
            }
            catch (Exception ex)
            {
                LogEvent($"Ошибка при отправке сообщения: {ex.Message}", ConsoleColor.Red);
            }

            LogMessage($" Я: {message}", ConsoleColor.White);
        }

        // Цикл обработки ввода пользователя
        private async Task HandleUserInputAsync(CancellationToken token)
        {
            LogEvent("Введите сообщение (или 'exit' для выхода):", ConsoleColor.Gray);

            while (!token.IsCancellationRequested)
            {
                string input = await Task.Run(() => Console.ReadLine(), token);
                if (string.IsNullOrWhiteSpace(input)) continue;

                if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Stop();
                    break;
                }

                await SendToAllPeersAsync(input);
            }
        }
    }

    // Точка входа в программу
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Разбор аргументов командной строки
            IPAddress localIP = IPAddress.Loopback;
            string localName = "User" + new Random().Next(1000);

            if (args.Length >= 1)
            {
                if (!IPAddress.TryParse(args[0], out localIP))
                {
                    Console.WriteLine("Неверный IP-адрес. Используется 127.0.0.1");
                    localIP = IPAddress.Loopback;
                }
            }
            if (args.Length >= 2)
            {
                localName = args[1];
                // Проверка, что имя не пустое
                if (string.IsNullOrWhiteSpace(localName))
                {
                    localName = "User" + new Random().Next(1000);
                    Console.WriteLine($"Имя не может быть пустым, используется {localName}");
                }
            }

            Console.WriteLine($"Запуск чата. IP: {localIP}, Имя: {localName}");

            var node = new ChatNode(localIP, localName);

            // Обработка Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                node.Stop();
                Environment.Exit(0);
            };

            try
            {
                await node.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                node.Stop();
            }
        }
    }
}