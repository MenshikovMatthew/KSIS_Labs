using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArmWrestlingGame.Network
{
    // Типы пакетов для синхронизации игровых событий между клиентом и сервером
    public enum PacketType
    {
        Matchmake,
        Found,
        SyncStart,
        HitEvent,
        StateUpdate,
        GameEnd,
        RematchRequest,
        RematchAccept,
        RematchDeny
    }

    // Структура пакета для передачи данных (тип события и полезная нагрузка в JSON)
    public class GamePacket
    {
        public PacketType Type { get; set; }
        public string Payload { get; set; }
    }

    public class GameClient
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        public bool IsConnected => client?.Connected ?? false;

        public event Action<string> OnMatchFound;
        public event Action<string> OnOpponentHit;
        public event Action OnGameStart;
        public event Action<float, float> OnStateUpdated;
        public event Action<string> OnGameEnded;
        public event Action OnRematchAccepted;

        /// <summary>
        /// Подключается к серверу и инициализирует потоки для обмена данными.
        /// Запускает фоновую задачу для прослушивания входящих пакетов.
        /// </summary>
        /// <returns>true если подключение успешно, false в противном случае</returns>
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(ip, port);

                var stream = client.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream) { AutoFlush = true };

                _ = ListenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Фоновая задача для непрерывного прослушивания входящих пакетов от сервера.
        /// Десериализует JSON и обрабатывает события через HandlePacket.
        /// </summary>
        private async Task ListenAsync()
        {
            try
            {
                while (IsConnected)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) break;

                    var packet = JsonSerializer.Deserialize<GamePacket>(line);
                    HandlePacket(packet);
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Обрабатывает входящие пакеты и вызывает соответствующие события UI.
        /// </summary>
        private void HandlePacket(GamePacket packet)
        {
            switch (packet.Type)
            {
                case PacketType.Found:
                    OnMatchFound?.Invoke(packet.Payload);
                    break;
                case PacketType.SyncStart:
                    OnGameStart?.Invoke();
                    break;
                case PacketType.HitEvent:
                    OnOpponentHit?.Invoke(packet.Payload);
                    break;
                case PacketType.StateUpdate:
                    var state = JsonSerializer.Deserialize<float[]>(packet.Payload);
                    if (state != null && state.Length >= 2)
                        OnStateUpdated?.Invoke(state[0], state[1]);
                    break;
                case PacketType.GameEnd:
                    OnGameEnded?.Invoke(packet.Payload);
                    break;
                case PacketType.RematchAccept:
                    OnRematchAccepted?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Отправляет пакет на сервер с указанным типом и полезной нагрузкой.
        /// </summary>
        public void SendPacket(PacketType type, object payload = null)
        {
            if (!IsConnected) return;
            var packet = new GamePacket { Type = type, Payload = payload != null ? JsonSerializer.Serialize(payload) : "" };
            writer.WriteLine(JsonSerializer.Serialize(packet));
        }

        /// Закрывает соединение с сервером
        public void Disconnect()
        {
            client?.Close();
        }
    }
}