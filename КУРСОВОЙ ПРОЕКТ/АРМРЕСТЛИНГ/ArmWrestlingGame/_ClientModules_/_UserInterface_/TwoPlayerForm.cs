using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArmWrestlingGame.Network;
using System.Diagnostics;

namespace ArmWrestlingGame
{
    public partial class TwoPlayerForm : Form
    {
        private string currentUser;
        private Form mainMenu;

        private MatchStats matchStats;
        private GameClient networkClient;
        private bool isOnlineMode = false;
        private string serverIp = "127.0.0.1";

        // Статистика текущего матча (не сохраняется)
        private int sessionHits = 0;
        private int sessionMisses = 0;
        private int sessionMaxCombo = 0;
        private int opponentMaxCombo = 0;

        private Timer gameTimer;
        private DateTime lastFrameTime;

        private Timer searchTimer;
        private float searchElapsed = 0f;

        // Состояния игры
        private enum GameState { Idle, Searching, Found, Countdown, Playing, Ended }
        private GameState currentState = GameState.Idle;
        private float stateTimer = 0f;
        private int countdownValue = 3;
        private string opponentName = "Opponent";
        private string endMatchMessage = "";
        private Color endMatchColor = Color.White;

        // Параметры реванша
        private bool isRematchTriggered = false;
        private float rematchTimer = 5.0f;
        private bool hasVotedRematch = false;

        // Время и баланс матча
        private float matchTimer = 60f;
        private int lastCountdownSec = -1;
        private int matchBalance = 0;
        private const int WinScore = 25;

        // Наклон рук и визуализация баланса
        private float targetTilt = 0f;
        private float currentTilt = 0f;

        // Параметры штрафного состояния
        private bool isPenaltyState = false;
        private float penaltyFadeAlpha = 255f;
        private float playerBarAlpha = 0f;
        private float oppBarAlpha = 0f;
        private float blinkTimer = 0f;
        private bool isArrowVisible = true;

        // Параметры вращения стрелки
        private float handAngle = -90f;
        private float currentSpeed = 150f;
        private int hitZoneCount = 0;
        private float targetStartAngle;
        private float deltaAngle = 22.5f;
        private Random rand = new Random();

        // Визуализация попаданий
        private List<Color> hitColors = new List<Color>();
        private List<Color> opponentHitColors = new List<Color>();
        private int combo = 0;

        // Анимационные параметры
        private float comboAnimScale = 1.0f;
        private float hitCircleAnimScale = 1.0f;
        private float flashAlpha = 0f;
        private float? missAngle = null;
        private DateTime missTime;

        // Параметры темы оформления
        private bool isAltTheme = false;
        private Color lineColor = Color.FromArgb(240, 240, 240);
        private Color outerFillColor = Color.FromArgb(45, 45, 45);
        private Color innerFillColor = Color.Black;
        private Color missColor = Color.FromArgb(204, 255, 76, 102);

        private Color[] origColors = { Color.Red, Color.OrangeRed, Color.Orange, Color.Gold, Color.Yellow };
        private Color[] altColors;

        /// <summary>
        /// Инициализирует двухигровую форму с поддержкой сети.
        /// Загружает статистику матчей, устанавливает полноэкранный режим и запускает игровой цикл.
        /// </summary>
        public TwoPlayerForm(string username, Form menu, string ip)
        {
            currentUser = username;
            mainMenu = menu;
            serverIp = ip;

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;
            this.KeyPreview = true;

            altColors = new Color[] {
            ColorTranslator.FromHtml("#feba53"), ColorTranslator.FromHtml("#8a5438"),
            ColorTranslator.FromHtml("#05355d"), ColorTranslator.FromHtml("#15608b"),
            ColorTranslator.FromHtml("#549abe")
            };

            matchStats = PlayerDataManager.LoadMatchStats(currentUser);

            InitializeComponent();
            InitNetwork();

            gameTimer = new Timer { Interval = 16 };
            gameTimer.Tick += GameLoop;
            lastFrameTime = DateTime.Now;
            gameTimer.Start();
        }

        /// <summary>
        /// Инициализирует сетевого клиента и подписывает обработчики событий сети.
        /// Обрабатывает входящие пакеты от сервера: попадания, синхронизацию и завершение игры.
        /// </summary>
        private void InitNetwork()
        {
            networkClient = new GameClient();
            _ = TryConnectWithRetryAsync();

            // Обработчик события поиска матча найден
            networkClient.OnMatchFound += (oppName) =>
            {
                this.Invoke((MethodInvoker)delegate {
                    searchTimer?.Stop();
                    opponentName = oppName.Replace("\"", "");
                    StartMatchSequence(false);
                });
            };

            // Обработчик события реванш принят
            networkClient.OnRematchAccepted += () =>
            {
                this.Invoke((MethodInvoker)delegate {
                    StartMatchSequence(true);
                });
            };

            // Обработчик удара противника
            networkClient.OnOpponentHit += (hitPayload) =>
            {
                this.Invoke((MethodInvoker)delegate {
                    if (currentState != GameState.Playing) return;
                    try
                    {
                        var hitData = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(hitPayload);
                        if (hitData != null && hitData.TryGetValue("ColorHex", out var colorHexObj) && hitData.TryGetValue("Delta", out var deltaObj))
                        {
                            string colorHex = colorHexObj?.ToString() ?? "#808080";
                            int.TryParse(deltaObj?.ToString() ?? "0", out int delta);

                            matchBalance += delta;

                            Color oppColor = ColorTranslator.FromHtml(colorHex);
                            if (opponentHitColors.Count >= 5) opponentHitColors.RemoveAt(0);
                            opponentHitColors.Add(oppColor);

                            UpdateMatchState();
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"[Client] Error parsing hit: {ex.Message}"); }
                });
            };

            // Обработчик синхронизации комбо противника
            networkClient.OnStateUpdated += (tilt, oppCombo) =>
            {
                this.Invoke((MethodInvoker)delegate {
                    this.opponentMaxCombo = (int)oppCombo;
                    Invalidate();
                });
            };

            // Обработчик завершения игры противником
            networkClient.OnGameEnded += (reason) =>
            {
                this.Invoke((MethodInvoker)delegate {
                    string cleanReason = reason?.Replace("\"", "").Trim();

                    if (cleanReason == "PLAYER_IS_ONLINE")
                    {
                        MessageBox.Show("Player is online!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                        return;
                    }

                    if (currentState == GameState.Playing || currentState == GameState.Countdown || currentState == GameState.Ended)
                    {
                        if (cleanReason == "Surrender")
                            EndMatch("OPPONENT LEFT", Color.Gold);
                        else if (cleanReason == "Defeat")
                            EndMatch("DEFEAT", Color.Red);
                    }
                });
            };
        }

        /// <summary>
        /// Пытается подключиться к серверу несколько раз с задержками между попытками.
        /// При первой успешной попытке включает режим онлайн.
        /// </summary>
        private async Task TryConnectWithRetryAsync()
        {
            await Task.Delay(1500);
            
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (networkClient.IsConnected) break;

                    isOnlineMode = await networkClient.ConnectAsync(serverIp, 5000);
                    if (isOnlineMode)
                    {
                        Console.WriteLine($"[Client] Connected to server at {serverIp} (attempt {attempt + 1})");
                        break;
                    }

                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Client] Connection attempt {attempt + 1} failed: {ex.Message}");
                }
            }
            
            if (!isOnlineMode)
            {
                Console.WriteLine("[Client] Failed to connect to server, will use offline mode");
            }
        }

        /// <summary>
        /// Пытается установить подключение к серверу (одна попытка).
        /// </summary>
        private async Task TryConnectAsync()
        {
            if (!networkClient.IsConnected)
            {
                try
                {
                    isOnlineMode = await networkClient.ConnectAsync(serverIp, 5000);
                    if (isOnlineMode)
                    {
                        Console.WriteLine($"[Client] Connected to server at {serverIp}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Client] Connection error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Обработчик кнопки "Найти матч": переводит в режим поиска и отправляет запрос на сервер.
        /// При таймауте или отсутствии сервера запускает игру с ботом.
        /// </summary>
        private async void BtnSearch_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            btnSearch.Visible = false;
            currentState = GameState.Searching;
            searchElapsed = 0f;

            if (!networkClient.IsConnected)
            {
                await TryConnectAsync();
            }

            if (networkClient.IsConnected && isOnlineMode)
            {
                Console.WriteLine("[Client] Searching for match online...");
                try
                {
                    networkClient.SendPacket(PacketType.Matchmake, currentUser);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Client] Error sending matchmake packet: {ex.Message}");
                }

                // Таймер поиска на 20 секунд
                searchTimer = new Timer { Interval = 1000 };
                searchTimer.Tick += (s, ev) => {
                    searchElapsed += 1.0f;
                    if (searchElapsed >= 20f)
                    {
                        if (searchTimer != null)
                        {
                            searchTimer.Stop();
                            searchTimer.Dispose();
                            searchTimer = null;
                        }
                        StopSearchAndStartBot();
                    }
                };
                searchTimer.Start();
            }
            else
            {
                Console.WriteLine("[Client] No server connection, starting offline bot game");
                StartBotGame();
            }

            this.ActiveControl = null;
        }

        // Останавливает поиск и запускает игру с ботом
        private void StopSearchAndStartBot()
        {
            Console.WriteLine("Matchmaking timeout. Starting bot...");
            StartBotGame();
        }

        // Запускает игру против тренировочного бота
        private void StartBotGame()
        {
            SoundManager.PlayFound();
            opponentName = "Training Bot";
            StartMatchSequence(false);
        }

        /// <summary>
        /// Обработчик кнопки "Реванш": фиксирует голос за реванш и отправляет запрос на сервер.
        /// В офлайн режиме имитирует согласие бота.
        /// </summary>
        private void BtnRematch_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            hasVotedRematch = true;
            btnRematch.Text = "WAITING...";
            btnRematch.FlatAppearance.BorderColor = Color.Gray;
            btnRematch.ForeColor = Color.Gray;

            if (isOnlineMode)
            {
                networkClient.SendPacket(PacketType.RematchRequest);
            }
            else
            {
                // Имитация согласия бота через 1 секунду
                Timer t = new Timer { Interval = 1000 };
                t.Tick += (s, ev) => { StartMatchSequence(true); t.Stop(); };
                t.Start();
            }
        }

        /// <summary>
        /// Инициализирует новый матч: сбрасывает статистику, анимации и состояния.
        /// Устанавливает начальную фазу "Found" для счётдауна перед боем.
        /// </summary>
        private void StartMatchSequence(bool isRematch)
        {
            this.Invoke((MethodInvoker)delegate {
                sessionHits = 0;
                sessionMisses = 0;
                sessionMaxCombo = 0;

                hitColors.Clear();
                opponentHitColors.Clear();
                combo = 0;
                currentSpeed = 150f;
                handAngle = -90f;
                hitZoneCount = 0;

                matchBalance = 0;
                matchTimer = 60f;
                lastCountdownSec = -1;
                targetTilt = 0f;
                currentTilt = 0f;
                playerBarAlpha = 0f;
                oppBarAlpha = 0f;

                isRematchTriggered = isRematch;
                currentState = GameState.Found;
                stateTimer = 2.0f;

                btnRematch.Visible = false;
                hasVotedRematch = false;
                btnRematch.Text = "REMATCH";
                btnRematch.FlatAppearance.BorderColor = Color.Gold;
                btnRematch.ForeColor = Color.Gold;
                this.ActiveControl = null;
            });
        }

        /// <summary>
        /// Обрабатывает нажатие клавиши Space: проверяет попадание во время игры.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space)
            {
                if (currentState == GameState.Playing && !isPenaltyState)
                {
                    CheckHit();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Запускает игру: инициализирует первую цель и счётчик попаданий
        private void StartGame()
        {
            hitZoneCount = 5;
            handAngle = -90f;
            isPenaltyState = false;
            GenerateNewTarget();
        }

        // Генерирует случайный угол для новой целевой зоны
        private void GenerateNewTarget() => targetStartAngle = rand.Next(0, 360);

        /// <summary>
        /// Основной игровой цикл: обновляет состояния игры, анимации, счётчик времени.
        /// Обрабатывает переходы между фазами игры (поиск, обратный отсчёт, игра, конец).
        /// </summary>
        private void GameLoop(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            float deltaTime = (float)(now - lastFrameTime).TotalSeconds;
            lastFrameTime = now;

            // Фаза "Найден" - переход к обратному отсчёту
            if (currentState == GameState.Found)
            {
                stateTimer -= deltaTime;
                if (stateTimer <= 0)
                {
                    currentState = GameState.Countdown;
                    stateTimer = 1.0f;
                    countdownValue = 3;
                    handAngle = -90f;
                }
            }
            // Фаза обратного отсчёта перед игрой
            else if (currentState == GameState.Countdown)
            {
                stateTimer -= deltaTime;
                if (stateTimer <= 0)
                {
                    countdownValue--;
                    stateTimer = 1.0f;
                    if (countdownValue < 0)
                    {
                        currentState = GameState.Playing;
                        StartGame();
                    }
                }
            }
            // Основная фаза игры
            else if (currentState == GameState.Playing)
            {
                matchTimer -= deltaTime;

                // Звук отсчёта последних 5 секунд
                int currentSec = (int)Math.Ceiling(matchTimer);
                if (currentSec <= 5 && currentSec > 0 && currentSec != lastCountdownSec)
                {
                    SoundManager.PlayCountdown();
                    lastCountdownSec = currentSec;
                }

                // Завершение матча по времени
                if (matchTimer <= 0)
                {
                    matchTimer = 0;
                    EndMatch("DRAW", Color.Gray);
                }

                // Плавное интерполирование наклона руки
                currentTilt += (targetTilt - currentTilt) * 5f * deltaTime;

                // Появление полос попаданий
                if (hitColors.Count > 0 && playerBarAlpha < 255f && !isPenaltyState)
                {
                    playerBarAlpha += 1000f * deltaTime;
                    if (playerBarAlpha > 255f) playerBarAlpha = 255f;
                }
                if (opponentHitColors.Count > 0 && oppBarAlpha < 255f)
                {
                    oppBarAlpha += 1000f * deltaTime;
                    if (oppBarAlpha > 255f) oppBarAlpha = 255f;
                }

                // Состояние штрафа после промаха
                if (isPenaltyState)
                {
                    penaltyFadeAlpha -= 1200f * deltaTime;
                    if (penaltyFadeAlpha < 0) penaltyFadeAlpha = 0;

                    // Мигание стрелки
                    blinkTimer += deltaTime;
                    if (blinkTimer >= 0.10f)
                    {
                        isArrowVisible = !isArrowVisible;
                        blinkTimer = 0f;
                    }

                    // Выход из штрафного состояния через 500ms
                    if (missAngle.HasValue && (now - missTime).TotalMilliseconds >= 500)
                    {
                        isPenaltyState = false;
                        missAngle = null;
                        isArrowVisible = true;
                        handAngle = -90f;
                        penaltyFadeAlpha = 255f;
                        playerBarAlpha = 0f;
                        hitColors.Clear();
                        hitZoneCount = 5;
                        currentSpeed = 150f;
                        combo = 0;
                        GenerateNewTarget();
                    }
                }
                else
                {
                    // Вращение стрелки во время игры
                    handAngle += currentSpeed * deltaTime;
                    handAngle %= 360f;

                    // Затухание вспышки
                    if (flashAlpha > 0f)
                    {
                        flashAlpha -= deltaTime * 1200f;
                        if (flashAlpha < 0f) flashAlpha = 0f;
                    }
                    // Сжатие комбо текста
                    if (comboAnimScale > 1.0f)
                    {
                        comboAnimScale -= deltaTime * 3.0f;
                        if (comboAnimScale < 1.0f) comboAnimScale = 1.0f;
                    }
                    // Сжатие кружка попадания
                    if (hitCircleAnimScale > 1.0f)
                    {
                        hitCircleAnimScale -= deltaTime * 3.0f;
                        if (hitCircleAnimScale < 1.0f) hitCircleAnimScale = 1.0f;
                    }
                }
            }
            // Фаза конца матча
            else if (currentState == GameState.Ended)
            {
                currentTilt += (targetTilt - currentTilt) * 10f * deltaTime;

                rematchTimer -= deltaTime;
                if (rematchTimer <= 0)
                {
                    currentState = GameState.Idle;
                    btnRematch.Visible = false;
                    btnSearch.Visible = true;
                    if (isOnlineMode) networkClient.SendPacket(PacketType.GameEnd);
                }
            }

            Invalidate();
        }

        /// <summary>
        /// Проверяет попадание в целевую зону. При попадании увеличивает комбо, баланс и скорость.
        /// При промахе переводит в штрафное состояние и отправляет данные на сервер.
        /// </summary>
        private void CheckHit()
        {
            float normalizedHand = (handAngle % 360 + 360) % 360;
            float relativeAngle = normalizedHand - targetStartAngle;
            if (relativeAngle < 0) relativeAngle += 360f;

            float totalTargetSweep = hitZoneCount * deltaAngle;

            if (relativeAngle <= totalTargetSweep)
            {
                // Успешное попадание
                SoundManager.PlayHit();
                sessionHits++;
                combo++;
                if (combo > sessionMaxCombo) sessionMaxCombo = combo;

                // Масштабирование визуализации по комбо
                comboAnimScale = (combo >= 20) ? 2.0f : (combo >= 10) ? 1.8f : (combo >= 5) ? 1.6f : 1.4f;
                hitCircleAnimScale = 1.6f;
                flashAlpha = 255f;

                // Определение цвета и урона
                int hitSegmentIndex = (int)(relativeAngle / deltaAngle);
                int colorArrayIdx = 4 - (5 - hitZoneCount + hitSegmentIndex);
                if (colorArrayIdx < 0) colorArrayIdx = 0;
                if (colorArrayIdx > 4) colorArrayIdx = 4;

                int points = 5 - colorArrayIdx;
                matchBalance -= points;

                Color hitColor = isAltTheme ? altColors[colorArrayIdx] : origColors[colorArrayIdx];

                // Отправка удара на сервер
                if (isOnlineMode)
                {
                    var hitData = new { ColorHex = ColorTranslator.ToHtml(hitColor), Delta = points };
                    networkClient.SendPacket(PacketType.HitEvent, hitData);
                    networkClient.SendPacket(PacketType.StateUpdate, new float[] { targetTilt, (float)sessionMaxCombo });
                }

                UpdateMatchState();

                if (hitColors.Count >= 5) hitColors.RemoveAt(0);
                hitColors.Add(hitColor);

                // Уменьшение размера целевой зоны
                if (hitZoneCount > 1) hitZoneCount--;

                // Увеличение скорости вращения
                if (combo >= 20) currentSpeed = 360f;
                else if (combo >= 10) currentSpeed = 300f;
                else if (combo >= 5) currentSpeed = 250f;
                else currentSpeed = 150f + (combo * 25f);

                GenerateNewTarget();
            }
            else
            {
                // Промах
                SoundManager.PlayMiss();
                sessionMisses++;
                matchBalance += 2;

                if (isOnlineMode)
                {
                    var missData = new { ColorHex = "#808080", Delta = -2 };
                    networkClient.SendPacket(PacketType.HitEvent, missData);
                }

                UpdateMatchState();

                isPenaltyState = true;
                playerBarAlpha = 0f;
                hitZoneCount = 0;
                missAngle = normalizedHand;
                missTime = DateTime.Now;
                blinkTimer = 0f;
            }
        }

        /// <summary>
        /// Обновляет состояние матча: рассчитывает наклон руки и проверяет победу/поражение.
        /// </summary>
        private void UpdateMatchState()
        {
            if (currentState != GameState.Playing) return;

            targetTilt = ((float)matchBalance / WinScore) * 90f;

            if (matchBalance <= -WinScore)
            {
                targetTilt = -90f;
                if (isOnlineMode) networkClient.SendPacket(PacketType.GameEnd, "Defeat");

                EndMatch("VICTORY", Color.LimeGreen);
            }
            else if (matchBalance >= WinScore)
            {
                targetTilt = 90f;
                EndMatch("DEFEAT", Color.Red);
            }
        }

        /// <summary>
        /// Завершает матч: обновляет статистику, воспроизводит звук результата и показывает экран реванша.
        /// </summary>
        private void EndMatch(string message, Color color)
        {
            currentState = GameState.Ended;
            endMatchMessage = message;
            endMatchColor = color;

            // Обновление статистики и звуки
            if (message == "VICTORY") { SoundManager.PlayVictory(); matchStats.Wins++; }
            else if (message == "DEFEAT") { SoundManager.PlayDefeat(); matchStats.Losses++; }
            else if (message == "OPPONENT LEFT") { SoundManager.PlayVictory(); matchStats.Wins++; }
            else { SoundManager.PlayDraw(); matchStats.Draws++; }

            PlayerDataManager.SaveMatchStats(currentUser, matchStats);

            // Пауза перед показом кнопки реванша
            Timer pauseTimer = new Timer { Interval = 3000 };
            pauseTimer.Tick += (s, e) => {
                pauseTimer.Stop();

                if (message == "OPPONENT LEFT")
                {
                    this.Invoke((MethodInvoker)delegate {
                        currentState = GameState.Idle;
                        btnRematch.Visible = false;
                        btnSearch.Visible = true;
                    });
                }
                else
                {
                    ShowRematchUI();
                }
            };
            pauseTimer.Start();
        }

        // Показывает кнопку реванша и запускает таймер ожидания
        private void ShowRematchUI()
        {
            this.Invoke((MethodInvoker)delegate {
                btnRematch.Visible = true;
                rematchTimer = 10.0f;
                hasVotedRematch = false;
            });
        }

        /// <summary>
        /// Определяет цвет сегмента целевой зоны по индексу и текущей теме.
        /// </summary>
        private Color GetSegmentColor(int segmentIndex, int currentZones)
        {
            int colorArrayIdx = 4 - (5 - currentZones + segmentIndex);
            if (colorArrayIdx < 0) colorArrayIdx = 0;
            if (colorArrayIdx > 4) colorArrayIdx = 4;
            return isAltTheme ? altColors[colorArrayIdx] : origColors[colorArrayIdx];
        }

        /// <summary>
        /// Создаёт GraphicsPath с закруглёнными углами для рисования скруглённых прямоугольников.
        /// </summary>
        private GraphicsPath GetRoundedRectPath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Отрисовывает всю игровую сцену в зависимости от текущего состояния.
        /// Включает статистику, таймер, целевую зону, руки, полосы попаданий и комбо.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = this.Width / 2;
            int cy = this.Height / 2;

            // Экран поиска с пульсирующим текстом
            if (currentState == GameState.Searching)
            {
                int alpha = (int)((Math.Sin(DateTime.Now.TimeOfDay.TotalMilliseconds / 200.0) + 1.0) / 2.0 * 255);
                using (Font f = new Font("Consolas", 36, FontStyle.Bold))
                using (Brush b = new SolidBrush(Color.FromArgb(alpha, Color.LimeGreen)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Searching...", f, b, new RectangleF(0, cy - 100, this.Width, 200), sf);
                }
            }

            // Отображение таймера матча
            if (currentState == GameState.Playing || currentState == GameState.Countdown)
            {
                int min = (int)(matchTimer / 60);
                int sec = (int)(matchTimer % 60);
                using (Font fTimer = new Font("Consolas", 38, FontStyle.Bold))
                {
                    g.DrawString($"{min:D2}:{sec:D2}", fTimer, Brushes.White, new PointF(220, 50));
                }
            }

            // Панель статистики в режиме ожидания (главное меню двухигровой)
            if (currentState == GameState.Idle)
            {
                int statsW = 120;
                int statsH = 260;
                int statsY = 20;
                int matchStatsX = this.Width - statsW - 20;

                using (GraphicsPath matchPath = GetRoundedRectPath(new RectangleF(matchStatsX, statsY, statsW, statsH), 10f))
                using (Brush fillBrush = new SolidBrush(Color.FromArgb(30, 30, 30)))
                using (Pen strokePen = new Pen(Color.LimeGreen, 2f))
                {
                    g.FillPath(fillBrush, matchPath);
                    g.DrawPath(strokePen, matchPath);
                }

                using (Font fLabel = new Font("Consolas", 12, FontStyle.Regular))
                using (Font fValue = new Font("Consolas", 22, FontStyle.Bold))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

                    int yOffset = statsY + 20;
                    g.DrawString("WINS", fLabel, Brushes.Gray, new RectangleF(matchStatsX, yOffset, statsW, 30), sf);
                    g.DrawString(matchStats.Wins.ToString(), fValue, Brushes.LimeGreen, new RectangleF(matchStatsX, yOffset + 20, statsW, 40), sf);

                    yOffset += 75;
                    g.DrawString("LOSSES", fLabel, Brushes.Gray, new RectangleF(matchStatsX, yOffset, statsW, 30), sf);
                    g.DrawString(matchStats.Losses.ToString(), fValue, Brushes.Red, new RectangleF(matchStatsX, yOffset + 20, statsW, 40), sf);

                    yOffset += 75;
                    g.DrawString("DRAWS", fLabel, Brushes.Gray, new RectangleF(matchStatsX, yOffset, statsW, 30), sf);
                    g.DrawString(matchStats.Draws.ToString(), fValue, Brushes.Gray, new RectangleF(matchStatsX, yOffset + 20, statsW, 40), sf);
                }
            }

            // Сообщение "Противник найден"
            if (currentState == GameState.Found)
            {
                int alpha = 255;
                if (stateTimer > 1.5f) alpha = (int)((2.0f - stateTimer) * 2f * 255);
                else if (stateTimer < 0.5f) alpha = (int)(stateTimer * 2f * 255);
                alpha = Math.Max(0, Math.Min(255, alpha));

                string text = isRematchTriggered ? $"Rematch\n{opponentName}" : $"Player found:\n{opponentName}";

                using (Font f = new Font("Consolas", 36, FontStyle.Bold))
                using (Brush b = new SolidBrush(Color.FromArgb(alpha, Color.LimeGreen)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, f, b, new RectangleF(0, cy - 100, this.Width, 200), sf);
                }
            }

            // Проверка показа игровых элементов
            bool showGameElements = (currentState == GameState.Countdown || currentState == GameState.Playing || (currentState == GameState.Ended && !btnRematch.Visible));

            if (showGameElements)
            {
                // Панель статистики текущего матча
                int statsW = 120;
                int statsH = 360;
                int statsY = 20;
                int hitsStatsX = this.Width - statsW - 20;

                using (GraphicsPath statsPath = GetRoundedRectPath(new RectangleF(hitsStatsX, statsY, statsW, statsH), 10f))
                using (Brush b = new SolidBrush(Color.FromArgb(30, 30, 30)))
                using (Pen p = new Pen(Color.Gray, 2f))
                {
                    g.FillPath(b, statsPath);
                    g.DrawPath(p, statsPath);
                }

                using (Font fLabel = new Font("Consolas", 12, FontStyle.Regular))
                using (Font fValue = new Font("Consolas", 22, FontStyle.Bold))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

                    int yOffset = statsY + 20;
                    g.DrawString("HITS", fLabel, Brushes.Gray, new RectangleF(hitsStatsX, yOffset, statsW, 30), sf);
                    g.DrawString(sessionHits.ToString(), fValue, Brushes.LimeGreen, new RectangleF(hitsStatsX, yOffset + 20, statsW, 40), sf);

                    yOffset += 80;
                    g.DrawString("MISSES", fLabel, Brushes.Gray, new RectangleF(hitsStatsX, yOffset, statsW, 30), sf);
                    g.DrawString(sessionMisses.ToString(), fValue, Brushes.OrangeRed, new RectangleF(hitsStatsX, yOffset + 20, statsW, 40), sf);

                    yOffset += 80;
                    g.DrawString("SPEED", fLabel, Brushes.Gray, new RectangleF(hitsStatsX, yOffset, statsW, 30), sf);
                    int displaySpeed = (currentState == GameState.Playing && !isPenaltyState) ? (int)currentSpeed : 0;
                    g.DrawString(displaySpeed.ToString(), fValue, Brushes.White, new RectangleF(hitsStatsX, yOffset + 20, statsW, 40), sf);

                    yOffset += 80;
                    g.DrawString("MAX COMBO", fLabel, Brushes.Gray, new RectangleF(hitsStatsX, yOffset, statsW, 30), sf);
                    g.DrawString(sessionMaxCombo.ToString(), fValue, Brushes.Gold, new RectangleF(hitsStatsX, yOffset + 20, statsW, 40), sf);
                }

                // Параметры отрисовки целевой зоны
                float scaleFactor = 8.75f;
                float innerRadius = Math.Min(this.Width, this.Height) / scaleFactor;
                float thickness = 12f;
                float lineWeight = 2f;
                float outerRadius = innerRadius + thickness + lineWeight / 2f;
                float middleRadius = innerRadius + thickness / 2f;
                float arcThickness = thickness - lineWeight / 2f;

                // Рисование руки с наклоном и вибрацией
                var armState = g.Save();
                g.TranslateTransform(cx, cy - outerRadius - 100);

                using (GraphicsPath platform = GetRoundedRectPath(new RectangleF(-100, -15, 200, 30), 10))
                using (Brush b = new SolidBrush(Color.FromArgb(50, 50, 50)))
                using (Pen p = new Pen(Color.Gray, 2f))
                {
                    g.FillPath(b, platform);
                    g.DrawPath(p, platform);
                }

                float wobble = (currentState == GameState.Playing) ? (float)Math.Sin(DateTime.Now.TimeOfDay.TotalMilliseconds / 150.0) * 1.5f : 0f;
                float actualDisplayTilt = Math.Max(-90f, Math.Min(90f, currentTilt));
                g.RotateTransform(actualDisplayTilt + wobble);

                Color armColor = actualDisplayTilt <= -20 ? Color.LimeGreen : (actualDisplayTilt >= 20 ? Color.Red : Color.White);

                using (GraphicsPath armPath = new GraphicsPath())
                using (Brush b = new SolidBrush(armColor))
                using (Pen p = new Pen(Color.DarkGray, 2f))
                {
                    armPath.AddPolygon(new PointF[] {
                new PointF(-14, 0),
                new PointF(-8, -150),
                new PointF(0, -170),
                new PointF(8, -150),
                new PointF(14, 0)
            });
                    g.FillPath(b, armPath);
                    g.DrawPath(p, armPath);
                }

                using (Brush bgBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
                using (Pen pLine = new Pen(armColor, 3f))
                using (Brush innerDotBrush = new SolidBrush(Color.DarkGray))
                {
                    g.FillEllipse(bgBrush, -25, -25, 50, 50);
                    g.DrawEllipse(pLine, -25, -25, 50, 50);
                    g.FillEllipse(innerDotBrush, -10, -10, 20, 20);
                }
                g.Restore(armState);

                // Рисование целевой зоны
                var state = g.Save();
                g.TranslateTransform(cx, cy);

                using (Pen linePen = new Pen(lineColor, lineWeight))
                using (Brush outerBrush = new SolidBrush(outerFillColor))
                using (Brush innerBrush = new SolidBrush(innerFillColor))
                {
                    g.FillEllipse(outerBrush, -outerRadius, -outerRadius, outerRadius * 2, outerRadius * 2);
                    g.DrawEllipse(linePen, -outerRadius, -outerRadius, outerRadius * 2, outerRadius * 2);
                    g.FillEllipse(innerBrush, -innerRadius, -innerRadius, innerRadius * 2, innerRadius * 2);
                    g.DrawEllipse(linePen, -innerRadius, -innerRadius, innerRadius * 2, innerRadius * 2);
                }

                // Рисование цветных дуг и разделяющих линий целевой зоны
                if (hitZoneCount > 0 && currentState == GameState.Playing)
                {
                    for (int i = 0; i < hitZoneCount; i++)
                    {
                        using (Pen arcPen = new Pen(GetSegmentColor(i, hitZoneCount), arcThickness))
                            g.DrawArc(arcPen, -middleRadius, -middleRadius, middleRadius * 2, middleRadius * 2, targetStartAngle + (i * deltaAngle), deltaAngle);
                    }
                    using (Pen bPen = new Pen(lineColor, lineWeight))
                    {
                        for (int i = 0; i <= hitZoneCount; i++)
                        {
                            float ang = (targetStartAngle + i * deltaAngle) * (float)Math.PI / 180f;
                            g.DrawLine(bPen, (float)Math.Cos(ang) * innerRadius, (float)Math.Sin(ang) * innerRadius, (float)Math.Cos(ang) * outerRadius, (float)Math.Sin(ang) * outerRadius);
                        }
                    }
                }

                // Рисование вращающейся стрелки
                if (isArrowVisible && currentState == GameState.Playing)
                {
                    float rad = handAngle * (float)Math.PI / 180f;
                    Color handC = isPenaltyState ? missColor : lineColor;
                    using (Pen hPen = new Pen(handC, lineWeight))
                    using (Brush hBrush = new SolidBrush(handC))
                    {
                        g.DrawLine(hPen, 0, 0, (float)Math.Cos(rad) * middleRadius, (float)Math.Sin(rad) * middleRadius);
                        g.FillEllipse(hBrush, -6, -6, 12, 12);
                    }
                }
                g.Restore(state);

                // Полосы попаданий игрока и противника
                int barWidth = 320;
                int barHeight = 60;
                int playerBarX = cx - (int)outerRadius - 40 - barWidth;
                int playerBarY = cy - barHeight / 2;
                int oppBarX = cx + (int)outerRadius + 40;
                int oppBarY = cy - barHeight / 2;

                // Полоса игрока
                int pAlpha = (int)Math.Min(penaltyFadeAlpha, playerBarAlpha);
                if (pAlpha > 0)
                {
                    using (GraphicsPath barPath = GetRoundedRectPath(new RectangleF(playerBarX, playerBarY, barWidth, barHeight), 15f))
                    using (Brush b = new SolidBrush(Color.FromArgb((int)(pAlpha * (30f / 255f)), 30, 30, 30)))
                    using (Pen p = new Pen(Color.FromArgb(pAlpha, Color.Gray), 3f))
                    {
                        g.FillPath(b, barPath);
                        g.DrawPath(p, barPath);
                    }

                    using (Font fName = new Font("Consolas", 16, FontStyle.Bold))
                    using (Brush bName = new SolidBrush(Color.FromArgb(pAlpha, Color.LimeGreen)))
                    {
                        StringFormat sfName = new StringFormat { Alignment = StringAlignment.Center };
                        g.DrawString(currentUser.ToUpper(), fName, bName, new RectangleF(playerBarX, playerBarY + barHeight + 10, barWidth, 30), sfName);
                    }

                    // Кружки попаданий игрока
                    for (int i = 0; i < hitColors.Count; i++)
                    {
                        bool isLastCircle = (i == hitColors.Count - 1 && !isPenaltyState && currentState == GameState.Playing);
                        float scale = isLastCircle ? hitCircleAnimScale : 1.0f;

                        var sC = g.Save();
                        g.TranslateTransform(playerBarX + 30 + (i * 55) + 20, playerBarY + 30);
                        g.ScaleTransform(scale, scale);

                        using (Brush b = new SolidBrush(Color.FromArgb(pAlpha, hitColors[i])))
                            g.FillEllipse(b, -20, -20, 40, 40);

                        using (Pen pLine = new Pen(Color.FromArgb(pAlpha, Color.White), 3f))
                            g.DrawEllipse(pLine, -20, -20, 40, 40);

                        if (isLastCircle && flashAlpha > 0f)
                        {
                            int fA = (int)(flashAlpha * (pAlpha / 255f));
                            if (fA > 0)
                                using (Brush fb = new SolidBrush(Color.FromArgb(fA, Color.White)))
                                    g.FillEllipse(fb, -20, -20, 40, 40);
                        }
                        g.Restore(sC);
                    }
                }

                // Полоса противника
                if (oppBarAlpha > 0)
                {
                    int oAlpha = (int)oppBarAlpha;
                    using (GraphicsPath oppBarPath = GetRoundedRectPath(new RectangleF(oppBarX, oppBarY, barWidth, barHeight), 15f))
                    using (Brush b = new SolidBrush(Color.FromArgb((int)(oAlpha * (30f / 255f)), 30, 30, 30)))
                    using (Pen p = new Pen(Color.FromArgb(oAlpha, Color.Gray), 3f))
                    {
                        g.FillPath(b, oppBarPath);
                        g.DrawPath(p, oppBarPath);
                    }

                    using (Font fName = new Font("Consolas", 16, FontStyle.Bold))
                    using (Brush bName = new SolidBrush(Color.FromArgb(oAlpha, Color.Red)))
                    {
                        StringFormat sfName = new StringFormat { Alignment = StringAlignment.Center };
                        g.DrawString(opponentName.ToUpper(), fName, bName, new RectangleF(oppBarX, oppBarY + barHeight + 10, barWidth, 30), sfName);
                    }

                    // Кружки попаданий противника
                    for (int i = 0; i < opponentHitColors.Count; i++)
                    {
                        var sC = g.Save();
                        g.TranslateTransform(oppBarX + 30 + (i * 55) + 20, oppBarY + 30);

                        using (Brush b = new SolidBrush(Color.FromArgb(oAlpha, opponentHitColors[i])))
                            g.FillEllipse(b, -20, -20, 40, 40);

                        using (Pen pLine = new Pen(Color.FromArgb(oAlpha, Color.White), 3f))
                            g.DrawEllipse(pLine, -20, -20, 40, 40);

                        g.Restore(sC);
                    }
                }

                // Текст комбо игрока
                if (combo > 0 && pAlpha > 0)
                {
                    Color baseC = combo >= 20 ? Color.Black : (combo >= 10 ? Color.Red : (combo >= 5 ? Color.Orange : Color.LimeGreen));
                    float baseSizeScale = combo >= 20 ? 1.75f : (combo >= 10 ? 1.5f : (combo >= 5 ? 1.35f : 1.2f));

                    var sCombo = g.Save();
                    g.TranslateTransform(cx, cy + outerRadius + 140);
                    float totalScale = baseSizeScale * comboAnimScale;
                    g.ScaleTransform(totalScale, totalScale);

                    using (GraphicsPath path = new GraphicsPath())
                    using (FontFamily ff = new FontFamily("Consolas"))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        path.AddString($"x{combo}", ff, (int)FontStyle.Bold, 56f, new Point(0, 0), sf);

                        using (Pen p = new Pen(Color.FromArgb(pAlpha, Color.White), 4f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
                        using (Brush b = new SolidBrush(Color.FromArgb(pAlpha, baseC))) g.FillPath(b, path);
                    }
                    g.Restore(sCombo);
                }
            }

            // Счётдаун перед началом игры
            if (currentState == GameState.Countdown)
            {
                DrawBigText(g, countdownValue > 0 ? countdownValue.ToString() : "START", Color.White, cx, cy);
            }
            // Результат матча
            else if (currentState == GameState.Ended)
            {
                if (!btnRematch.Visible)
                {
                    DrawBigText(g, endMatchMessage, endMatchColor, cx, cy);
                }
                else
                {
                    if (!hasVotedRematch)
                    {
                        using (Font fTimer = new Font("Consolas", 18, FontStyle.Bold))
                        {
                            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                            g.DrawString($"Closing session in {(int)Math.Ceiling(rematchTimer)}s...", fTimer, Brushes.Gray, new RectangleF(0, cy + 120, this.Width, 30), sf);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Рисует большой текст результата с чёрной обводкой для лучшей видимости.
        /// </summary>
        private void DrawBigText(Graphics g, string text, Color color, int cx, int cy)
        {
            using (GraphicsPath path = new GraphicsPath())
            using (FontFamily ff = new FontFamily("Consolas"))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                path.AddString(text, ff, (int)FontStyle.Bold, 150f, new RectangleF(0, cy - 100, this.Width, 200), sf);

                using (Pen p = new Pen(Color.Black, 6f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
                using (Brush b = new SolidBrush(color)) { g.FillPath(b, path); }
            }
        }

        // Обработчик кнопки переключения темы
        private void BtnTheme_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            isAltTheme = !isAltTheme;
            this.ActiveControl = null;
            Invalidate();
        }

        /// <summary>
        /// Обработчик кнопки выхода: останавливает игру, отправляет сигнал сдачи на сервер и закрывает форму.
        /// </summary>
        private void BtnExit_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            gameTimer.Stop();
            if (searchTimer != null) searchTimer.Stop();

            if (isOnlineMode && networkClient != null && networkClient.IsConnected)
            {
                networkClient.SendPacket(PacketType.GameEnd, "Surrender");
                networkClient.Disconnect();
            }

            this.Close();
        }

        /// <summary>
        /// Обработчик закрытия формы: гарантирует очистку ресурсов и отключение от сервера.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            gameTimer?.Stop();
            if (searchTimer != null) searchTimer.Stop();

            if (isOnlineMode && networkClient != null && networkClient.IsConnected)
            {
                networkClient.SendPacket(PacketType.GameEnd, "Surrender");
                networkClient.Disconnect();
            }

            if (mainMenu != null && !mainMenu.Visible)
            {
                mainMenu.Show();
            }
        }
    }
}