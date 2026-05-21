using System;

namespace ArmWrestlingServer
{
    public class GameMatch
    {
        // Ссылки на двух игроков в матче
        public ClientConnection Player1 { get; }
        public ClientConnection Player2 { get; }

        // Флаги согласия на реванш
        private bool p1Rematch = false;
        private bool p2Rematch = false;

        // Статус активности матча
        private bool _isMatchActive = false;
        // Событие при завершении матча
        public event Action<GameMatch> OnMatchEnded;

        /// <summary>
        /// Инициализирует матч между двумя игроками и подписывает обработчики пакетов и отключений.
        /// </summary>
        public GameMatch(ClientConnection p1, ClientConnection p2)
        {
            Player1 = p1;
            Player2 = p2;

            Player1.OnPacketReceived += HandlePacketFromP1;
            Player2.OnPacketReceived += HandlePacketFromP2;

            Player1.OnDisconnected += HandleP1Disconnect;
            Player2.OnDisconnected += HandleP2Disconnect;
        }

        /// <summary>
        /// Обрабатывает отключение первого игрока: уведомляет второго о победе и завершает матч.
        /// </summary>
        private void HandleP1Disconnect(ClientConnection client)
        {
            if (_isMatchActive)
            {
                Console.WriteLine($"[Match] Player 1 ({Player1.Username}) ({Player1.Id}) disconnected mid-match. {Player2.Username} ({Player2.Id}) wins by default.");
                if (Player2.IsConnected)
                {
                    Player2.Send(PacketType.GameEnd, "Surrender");
                }
                _isMatchActive = false;
            }
            else
            {
                Console.WriteLine($"[Match] Player 1 ({Player1.Username}) ({Player1.Id}) left the match session.");
            }
            EndMatch();
        }

        /// <summary>
        /// Обрабатывает отключение второго игрока: уведомляет первого о победе и завершает матч.
        /// </summary>
        private void HandleP2Disconnect(ClientConnection client)
        {
            if (_isMatchActive)
            {
                Console.WriteLine($"[Match] Player 2 ({Player2.Username}) ({Player2.Id}) disconnected mid-match. {Player1.Username} ({Player1.Id}) wins by default.");
                if (Player1.IsConnected)
                {
                    Player1.Send(PacketType.GameEnd, "Surrender");
                }
                _isMatchActive = false;
            }
            else
            {
                Console.WriteLine($"[Match] Player 2 ({Player2.Username}) ({Player2.Id}) left the match session.");
            }
            EndMatch();
        }

        /// <summary>
        /// Запускает матч: активирует его и отправляет обоим игрокам информацию об оппонентах.
        /// </summary>
        public void Start()
        {
            _isMatchActive = true;
            Console.WriteLine($"[Match] Started between {Player1.Username} ({Player1.Id}) and {Player2.Username} ({Player2.Id})");
            Player1.Send(PacketType.Found, Player2.Username);
            Player2.Send(PacketType.Found, Player1.Username);
        }

        // Маршрутизирует пакет от первого игрока ко второму
        private void HandlePacketFromP1(ClientConnection client, GamePacket packet) => RoutePacket(packet, Player2, Player1);
        // Маршрутизирует пакет от второго игрока к первому
        private void HandlePacketFromP2(ClientConnection client, GamePacket packet) => RoutePacket(packet, Player1, Player2);

        /// <summary>
        /// Маршрутизирует пакеты между игроками: обрабатывает реванши, удары, завершение матча.
        /// Контролирует состояние матча и логику побед/поражений.
        /// </summary>
        private void RoutePacket(GamePacket packet, ClientConnection target, ClientConnection sender)
        {
            if (packet.Type == PacketType.RematchRequest)
            {
                // Фиксируем согласие игрока на реванш
                if (sender == Player1) p1Rematch = true;
                if (sender == Player2) p2Rematch = true;

                // Если запрашивают реванш и матч активен, то это означает ничью (время вышло)
                if (_isMatchActive)
                {
                    Console.WriteLine($"[Match] Match ended in a DRAW between {Player1.Username} ({Player1.Id}) and {Player2.Username} ({Player2.Id}).");
                    _isMatchActive = false;
                }

                // Если оба согласили на реванш, запускаем новый матч
                if (p1Rematch && p2Rematch)
                {
                    p1Rematch = false;
                    p2Rematch = false;
                    _isMatchActive = true;
                    Player1.Send(PacketType.RematchAccept);
                    Player2.Send(PacketType.RematchAccept);
                    Console.WriteLine($"[Match] Rematch accepted between {Player1.Username} ({Player1.Id}) and {Player2.Username} ({Player2.Id})");
                }
            }
            else if (packet.Type == PacketType.HitEvent)
            {
                // Передаём удар противнику только если матч активен
                if (_isMatchActive)
                {
                    target.Send(packet.Type, packet.Payload);
                }
            }
            else if (packet.Type == PacketType.GameEnd)
            {
                string payload = packet.Payload ?? "";

                if (payload.Contains("Surrender"))
                {
                    // Сдача во время активного матча
                    if (_isMatchActive)
                    {
                        Console.WriteLine($"[Match] {sender.Username} ({sender.Id}) surrendered mid-match. {target.Username} ({target.Id}) wins by default.");
                        target.Send(PacketType.GameEnd, "Surrender");
                        _isMatchActive = false;
                    }
                    else
                    {
                        // Выход во время экрана реванша
                        Console.WriteLine($"[Match] {sender.Username} ({sender.Id}) left during the rematch screen.");
                    }
                    EndMatch();
                }
                else if (payload.Contains("Defeat"))
                {
                    // Отправитель пакета Defeat - это ПОБЕДИТЕЛЬ матча
                    if (_isMatchActive)
                    {
                        Console.WriteLine($"[Match] {sender.Username} ({sender.Id}) won the match! ({target.Username} ({target.Id}) was defeated).");
                        _isMatchActive = false;
                    }
                    target.Send(packet.Type, packet.Payload);
                }
                else
                {
                    // Пустой payload означает таймаут реванша
                    if (string.IsNullOrEmpty(payload.Replace("\"", "").Trim()))
                    {
                        Console.WriteLine($"[Match] Rematch timer expired. Session closed.");
                        EndMatch();
                    }
                }
            }
        }

        /// <summary>
        /// Завершает матч: отписывает обработчики событий и вызывает событие OnMatchEnded.
        /// </summary>
        private void EndMatch()
        {
            Console.WriteLine($"[Match] Ended and cleaned up between {Player1.Username} ({Player1.Id}) and {Player2.Username} ({Player2.Id})");

            Player1.OnPacketReceived -= HandlePacketFromP1;
            Player2.OnPacketReceived -= HandlePacketFromP2;

            Player1.OnDisconnected -= HandleP1Disconnect;
            Player2.OnDisconnected -= HandleP2Disconnect;

            OnMatchEnded?.Invoke(this);
        }
    }
}