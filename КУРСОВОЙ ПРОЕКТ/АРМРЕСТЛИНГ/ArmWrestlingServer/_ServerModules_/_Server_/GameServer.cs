using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ArmWrestlingServer
{
    public class GameServer
    {
        private TcpListener _listener;
        // Очередь клиентов в ожидании матча
        private ConcurrentQueue<ClientConnection> _matchmakingQueue = new ConcurrentQueue<ClientConnection>();
        // Словарь активных пользователей по ID соединения
        private ConcurrentDictionary<string, ClientConnection> _activeUsers = new ConcurrentDictionary<string, ClientConnection>();

        /// <summary>
        /// Запускает сервер на указанном порту.
        /// Определяет локальный IP-адрес и начинает ожидание подключений клиентов.
        /// </summary>
        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            // Определяем локальный IP-адрес
            string localIp = "127.0.0.1";
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        localIp = ip.ToString();
                        break;
                    }
                }
            }
            catch { }

            Console.WriteLine($"[Server] Started! IP Address: {localIp} | Port {port}. Waiting for connections...");

            _ = AcceptClientsAsync();
        }

        /// <summary>
        /// Асинхронно принимает входящие подключения клиентов и инициализирует их прослушивание.
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            try
            {
                while (true)
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                    var client = new ClientConnection(tcpClient);

                    Console.WriteLine($"[Server] Client connected: {client.Id}");

                    client.OnPacketReceived += HandleInitialPackets;
                    client.OnDisconnected += HandleDisconnect;

                    _ = client.StartListeningAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error in AcceptClientsAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Обрабатывает начальные пакеты от клиента: парсит имя пользователя и добавляет в очередь поиска.
        /// </summary>
        private void HandleInitialPackets(ClientConnection client, GamePacket packet)
        {
            if (packet.Type == PacketType.Matchmake)
            {
                string username = ParseUsername(packet.Payload);

                if (string.IsNullOrWhiteSpace(username))
                {
                    username = client.Id;
                }

                client.Username = username;
                Console.WriteLine($"[Server] Client {client.Username} ({client.Id}) is looking for a match.");

                // Сохраняем пользователя в список активных по ID соединения
                _activeUsers[client.Id] = client;

                _matchmakingQueue.Enqueue(client);
                TryCreateMatch();
            }
        }

        /// <summary>
        /// Парсит имя пользователя из payload пакета: убирает кавычки и пробелы.
        /// </summary>
        private string ParseUsername(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return null;

            try
            {
                string username = payload.Trim('"');
                return string.IsNullOrWhiteSpace(username) ? null : username;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Пытается создать матч из двух игроков в очереди поиска.
        /// Проверяет наличие соединения и управляет переходом в игровой сеанс.
        /// </summary>
        private void TryCreateMatch()
        {
            // Пока в очереди есть хотя бы 2 человека
            while (_matchmakingQueue.Count >= 2)
            {
                if (_matchmakingQueue.TryDequeue(out var p1))
                {
                    // Проверяем, не отключился ли первый игрок
                    if (!p1.IsConnected)
                    {
                        Console.WriteLine($"[Server] Player {p1.Username} ({p1.Id}) disconnected while waiting.");
                        _activeUsers.TryRemove(p1.Username, out _);
                        continue;
                    }

                    if (_matchmakingQueue.TryDequeue(out var p2))
                    {
                        // Проверяем второго игрока
                        if (!p2.IsConnected)
                        {
                            Console.WriteLine($"[Server] Player {p2.Username} ({p2.Id}) disconnected, returning {p1.Username} ({p1.Id}) to queue.");
                            // Возвращаем первого в очередь если второй отключился
                            _matchmakingQueue.Enqueue(p1);
                            _activeUsers.TryRemove(p2.Username, out _);
                            continue;
                        }

                        // Создаём матч между двумя активными игроками
                        Console.WriteLine($"[Server] Match created: {p1.Username} ({p1.Id}) vs {p2.Username} ({p2.Id})");

                        p1.OnPacketReceived -= HandleInitialPackets;
                        p2.OnPacketReceived -= HandleInitialPackets;

                        var match = new GameMatch(p1, p2);

                        // После завершения матча игроки могут искать новую игру
                        match.OnMatchEnded += (m) => {
                            if (m.Player1.IsConnected) m.Player1.OnPacketReceived += HandleInitialPackets;
                            if (m.Player2.IsConnected) m.Player2.OnPacketReceived += HandleInitialPackets;
                        };

                        match.Start();
                    }
                    else
                    {
                        // Возвращаем первого в очередь если второго нет
                        _matchmakingQueue.Enqueue(p1);
                        break;
                    }
                }
            }
        }

        // Обработчик отключения клиента: удаляет из активных и отписывает от событий
        private void HandleDisconnect(ClientConnection client)
        {
            if (client.Username != "Unknown")
            {
                Console.WriteLine($"[Server] Client disconnected: {client.Id} ({client.Username})");

                // Удаляем пользователя из активных по ID соединения
                _activeUsers.TryRemove(client.Id, out _);
            }

            client.OnPacketReceived -= HandleInitialPackets;
        }
    }
}