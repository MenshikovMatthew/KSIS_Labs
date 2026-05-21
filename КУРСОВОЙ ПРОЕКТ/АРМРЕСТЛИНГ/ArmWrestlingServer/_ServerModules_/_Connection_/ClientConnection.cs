using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArmWrestlingServer
{
    public class ClientConnection
    {
        // Уникальный ID подключения
        public string Id { get; } = Guid.NewGuid().ToString().Substring(0, 8);
        // Имя пользователя, подключившегося к серверу
        public string Username { get; set; } = "Unknown";
        // Статус подключения к серверу
        public bool IsConnected => _client != null && _client.Connected;
        
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;

        // События для обработки входящих пакетов и разрыва соединения
        public event Action<ClientConnection, GamePacket> OnPacketReceived;
        public event Action<ClientConnection> OnDisconnected;

        /// <summary>
        /// Инициализирует подключение клиента: получает поток данных и создаёт читатель/писатель.
        /// </summary>
        public ClientConnection(TcpClient client)
        {
            _client = client;
            try
            {
                var stream = _client.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientConnection] Error initializing streams: {ex.Message}");
            }
        }

        /// <summary>
        /// Асинхронно прослушивает входящие пакеты от клиента и вызывает обработчик при получении.
        /// При разрыве соединения автоматически отключается.
        /// </summary>
        public async Task StartListeningAsync()
        {
            try
            {
                while (_client != null && _client.Connected)
                {
                    string line = await _reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) break;

                    try
                    {
                        var packet = JsonSerializer.Deserialize<GamePacket>(line);
                        if (packet != null)
                        {
                            OnPacketReceived?.Invoke(this, packet);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"[ClientConnection] JSON parse error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientConnection] Error reading from client {Id}: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Отправляет пакет клиенту. Если payload - строка, передаёт как есть, иначе сериализует в JSON.
        /// </summary>
        public void Send(PacketType type, object payload = null)
        {
            if (_client == null || !_client.Connected)
            {
                Console.WriteLine($"[ClientConnection] Cannot send to {Id}: not connected");
                return;
            }

            try
            {
                string payloadString;

                // Предотвращение двойной сериализации JSON при передаче от оппонента
                if (payload is string s)
                {
                    payloadString = s;
                }
                else if (payload != null)
                {
                    payloadString = JsonSerializer.Serialize(payload);
                }
                else
                {
                    payloadString = "";
                }

                var packet = new GamePacket
                {
                    Type = type,
                    Payload = payloadString
                };

                _writer.WriteLine(JsonSerializer.Serialize(packet));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientConnection] Error sending to {Id}: {ex.Message}");
                Disconnect();
            }
        }

        /// <summary>
        /// Закрывает соединение и освобождает ресурсы потоков и TCP клиента.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _client?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientConnection] Error disconnecting {Id}: {ex.Message}");
            }

            OnDisconnected?.Invoke(this);
        }
    }
}