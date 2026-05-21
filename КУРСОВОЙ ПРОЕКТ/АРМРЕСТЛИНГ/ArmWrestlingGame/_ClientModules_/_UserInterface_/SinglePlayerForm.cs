using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ArmWrestlingGame
{
    public partial class SinglePlayerForm : Form
    {
        private string currentUser;
        private string statsFilePath;
        private PlayerStats currentStats;

        private Form mainMenu;
        private Timer gameTimer;
        private DateTime lastFrameTime;

        // Состояния игры и логика наказания
        private bool isGameStarted = false;
        private bool isPenaltyState = false;
        private float penaltyFadeAlpha = 255f;
        private float barFadeInAlpha = 0f;
        private float blinkTimer = 0f;
        private bool isArrowVisible = true;

        // Параметры вращения стрелки
        private float handAngle = -90f;
        private float currentSpeed = 150f;
        private float maxSpeed = 360f;

        // Параметры целей попаданий
        private int hitZoneCount = 0;
        private float targetStartAngle;
        private float deltaAngle = 22.5f;
        private Random rand = new Random();

        // Визуализация попаданий и комбо
        private List<Color> hitColors = new List<Color>();
        private int combo = 0;

        // Анимационные параметры
        private float comboAnimScale = 1.0f;
        private float hitCircleAnimScale = 1.0f;
        private float flashAlpha = 0f;

        // Параметры промаха
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
        /// Инициализирует одиночную игру, загружает статистику игрока и запускает игровой цикл.
        /// Устанавливает полноэкранный режим и обработчик нажатия клавиш для управления.
        /// </summary>
        public SinglePlayerForm(string username, Form menu)
        {
            InitializeComponent();
            currentUser = username;
            mainMenu = menu;

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

            LoadStats();
            ResetGameState(false);

            gameTimer = new Timer { Interval = 16 };
            gameTimer.Tick += GameLoop;
            lastFrameTime = DateTime.Now;
            gameTimer.Start();
        }

        // Загружает сохранённую статистику игрока
        private void LoadStats()
        {
            currentStats = PlayerDataManager.LoadStats(currentUser);
        }

        // Сохраняет текущую статистику в файл
        private void SaveStats()
        {
            PlayerDataManager.SaveStats(currentUser, currentStats);
        }

        // Обработчик кнопки "Остановить": сбрасывает состояние игры
        private void btnStop_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            ResetGameState(false);
            this.ActiveControl = null;
            Invalidate();
        }

        // Обработчик кнопки "Сбросить": очищает статистику и сбрасывает состояние
        private void btnReset_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            currentStats = new PlayerStats();
            SaveStats();
            ResetGameState(true);
            this.ActiveControl = null;
            Invalidate();
        }

        // Обработчик кнопки "Тема": переключает между основной и альтернативной темой
        private void btnTheme_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            isAltTheme = !isAltTheme;
            this.ActiveControl = null;
            Invalidate();
        }

        // Обработчик кнопки "Выход": завершает игру и возвращает в главное меню
        private void btnExit_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            gameTimer.Stop();
            SaveStats();

            mainMenu.Show();
            this.Close();
        }

        /// <summary>
        /// Обрабатывает нажатие клавиши Space: начинает игру или проверяет попадание.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space)
            {
                if (!isPenaltyState)
                {
                    if (!isGameStarted) StartGame();
                    else CheckHit();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// Сбрасывает состояние игры. Если resetStats = true, очищает статистику.
        /// </summary>
        private void ResetGameState(bool resetStats)
        {
            isGameStarted = false;
            isPenaltyState = false;
            handAngle = -90f;
            currentSpeed = 150f;
            hitZoneCount = 0;
            combo = 0;

            if (resetStats)
            {
                currentStats.TotalHits = 0;
                currentStats.TotalMisses = 0;
                currentStats.MaxCombo = 0;
            }

            comboAnimScale = 1.0f;
            hitCircleAnimScale = 1.0f;
            flashAlpha = 0f;
            penaltyFadeAlpha = 255f;
            barFadeInAlpha = 0f;
            hitColors.Clear();
            missAngle = null;
            isArrowVisible = true;
        }

        // Запускает игру, инициализирует первую цель
        private void StartGame()
        {
            isGameStarted = true;
            hitZoneCount = 5;
            GenerateNewTarget();
        }

        // Генерирует случайный угол для новой цели попадания
        private void GenerateNewTarget() => targetStartAngle = rand.Next(0, 360);

        /// <summary>
        /// Основной игровой цикл: обновляет анимации, состояние стрелки и логику наказания.
        /// Вычисляет deltaTime для плавного движения независимо от FPS.
        /// </summary>
        private void GameLoop(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            float deltaTime = (float)(now - lastFrameTime).TotalSeconds;
            lastFrameTime = now;

            // Полоса попаданий плавно появляется только при наличии попаданий
            if (isGameStarted && hitColors.Count > 0 && barFadeInAlpha < 255f && !isPenaltyState)
            {
                barFadeInAlpha += 1000f * deltaTime;
                if (barFadeInAlpha > 255f) barFadeInAlpha = 255f;
            }

            if (isPenaltyState)
            {
                penaltyFadeAlpha -= 1200f * deltaTime;
                if (penaltyFadeAlpha < 0) penaltyFadeAlpha = 0;

                // Мигание стрелки во время штрафа
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
                    barFadeInAlpha = 0f;
                    hitColors.Clear();
                    hitZoneCount = 5;
                    currentSpeed = 150f;
                    combo = 0;
                    GenerateNewTarget();
                }
            }
            else if (isGameStarted)
            {
                // Вращение стрелки
                handAngle += currentSpeed * deltaTime;
                handAngle %= 360f;

                // Затухание вспышки при попадании
                if (flashAlpha > 0f)
                {
                    flashAlpha -= deltaTime * 1200f;
                    if (flashAlpha < 0f) flashAlpha = 0f;
                }

                // Уменьшение масштаба комбо текста
                if (comboAnimScale > 1.0f)
                {
                    comboAnimScale -= deltaTime * 3.0f;
                    if (comboAnimScale < 1.0f) comboAnimScale = 1.0f;
                }

                // Уменьшение масштаба кружка попадания
                if (hitCircleAnimScale > 1.0f)
                {
                    hitCircleAnimScale -= deltaTime * 3.0f;
                    if (hitCircleAnimScale < 1.0f) hitCircleAnimScale = 1.0f;
                }
            }
            Invalidate();
        }

        /// <summary>
        /// Проверяет попадание в целевую зону. При попадании увеличивает комбо и скорость.
        /// При промахе переводит в штрафное состояние.
        /// </summary>
        private void CheckHit()
        {
            float normalizedHand = (handAngle % 360 + 360) % 360;
            float relativeAngle = normalizedHand - targetStartAngle;
            if (relativeAngle < 0) relativeAngle += 360f;

            float totalTargetSweep = hitZoneCount * deltaAngle;

            if (relativeAngle <= totalTargetSweep)
            {
                // Попадание: увеличиваем статистику и комбо
                SoundManager.PlayHit();
                currentStats.TotalHits++;
                combo++;
                if (combo > currentStats.MaxCombo) currentStats.MaxCombo = combo;

                // Масштабирование комбо по его значению
                comboAnimScale = (combo >= 20) ? 2.0f : (combo >= 10) ? 1.8f : (combo >= 5) ? 1.6f : 1.4f;
                hitCircleAnimScale = 1.6f;
                flashAlpha = 255f;

                // Определяем цвет попадания
                int hitSegmentIndex = (int)(relativeAngle / deltaAngle);
                Color hitColor = GetSegmentColor(hitSegmentIndex, hitZoneCount);

                if (hitColors.Count >= 5) hitColors.RemoveAt(0);
                hitColors.Add(hitColor);

                // Уменьшаем размер целевой зоны за попадание
                if (hitZoneCount > 1) hitZoneCount--;

                // Увеличиваем скорость в зависимости от комбо
                if (combo >= 20) currentSpeed = 360f;
                else if (combo >= 10) currentSpeed = 300f;
                else if (combo >= 5) currentSpeed = 250f;
                else currentSpeed = 150f + (combo * 25f);

                GenerateNewTarget();
            }
            else
            {
                // Промах: переходим в штрафное состояние
                SoundManager.PlayMiss();
                currentStats.TotalMisses++;
                isPenaltyState = true;
                hitZoneCount = 0;
                missAngle = normalizedHand;
                missTime = DateTime.Now;
                blinkTimer = 0f;
            }
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
        /// Отрисовывает всю игровую сцену: статистику, целевую зону, стрелку и полосу попаданий.
        /// Это центральная функция визуализации.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = this.Width / 2;
            int cy = this.Height / 2;

            float scaleFactor = 8.75f;
            float innerRadius = Math.Min(this.Width, this.Height) / scaleFactor;
            float thickness = 12f;
            float lineWeight = 2f;
            float outerRadius = innerRadius + thickness + lineWeight / 2f;
            float middleRadius = innerRadius + thickness / 2f;
            float arcThickness = thickness - lineWeight / 2f;

            // Рисуем боковую панель статистики
            int statsW = 120;
            int statsH = 360;
            int statsX = this.Width - statsW - 20;
            int statsY = 20;

            using (GraphicsPath statsPath = GetRoundedRectPath(new RectangleF(statsX, statsY, statsW, statsH), 10f))
            {
                g.FillPath(new SolidBrush(Color.FromArgb(30, 30, 30)), statsPath);
                g.DrawPath(new Pen(Color.Gray, 2f), statsPath);
            }

            using (Font fLabel = new Font("Consolas", 12, FontStyle.Regular))
            using (Font fValue = new Font("Consolas", 22, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                int yOffset = statsY + 20;

                g.DrawString("HITS", fLabel, Brushes.Gray, new RectangleF(statsX, yOffset, statsW, 30), sf);
                g.DrawString(currentStats.TotalHits.ToString(), fValue, Brushes.LimeGreen, new RectangleF(statsX, yOffset + 20, statsW, 40), sf);

                yOffset += 80;
                g.DrawString("MISSES", fLabel, Brushes.Gray, new RectangleF(statsX, yOffset, statsW, 30), sf);
                g.DrawString(currentStats.TotalMisses.ToString(), fValue, Brushes.OrangeRed, new RectangleF(statsX, yOffset + 20, statsW, 40), sf);

                yOffset += 80;
                g.DrawString("SPEED", fLabel, Brushes.Gray, new RectangleF(statsX, yOffset, statsW, 30), sf);
                int displaySpeed = (isGameStarted && !isPenaltyState) ? (int)currentSpeed : 0;
                g.DrawString(displaySpeed.ToString(), fValue, Brushes.White, new RectangleF(statsX, yOffset + 20, statsW, 40), sf);

                yOffset += 80;
                g.DrawString("MAX COMBO", fLabel, Brushes.Gray, new RectangleF(statsX, yOffset, statsW, 30), sf);
                g.DrawString(currentStats.MaxCombo.ToString(), fValue, Brushes.Gold, new RectangleF(statsX, yOffset + 20, statsW, 40), sf);
            }

            var state = g.Save();
            g.TranslateTransform(cx, cy);

            // Рисуем внешний и внутренний круги целевой зоны
            using (Pen linePen = new Pen(lineColor, lineWeight))
            using (Brush outerBrush = new SolidBrush(outerFillColor))
            using (Brush innerBrush = new SolidBrush(innerFillColor))
            {
                g.FillEllipse(outerBrush, -outerRadius, -outerRadius, outerRadius * 2, outerRadius * 2);
                g.DrawEllipse(linePen, -outerRadius, -outerRadius, outerRadius * 2, outerRadius * 2);
                g.FillEllipse(innerBrush, -innerRadius, -innerRadius, innerRadius * 2, innerRadius * 2);
                g.DrawEllipse(linePen, -innerRadius, -innerRadius, innerRadius * 2, innerRadius * 2);
            }

            // Рисуем цветные дуги целевой зоны и разделяющие линии
            if (hitZoneCount > 0)
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

            // Рисуем вращающуюся стрелку
            if (isArrowVisible)
            {
                float rad = handAngle * (float)Math.PI / 180f;
                Color handC = isPenaltyState ? missColor : lineColor;
                using (Pen hPen = new Pen(handC, lineWeight))
                {
                    g.DrawLine(hPen, 0, 0, (float)Math.Cos(rad) * middleRadius, (float)Math.Sin(rad) * middleRadius);
                    g.FillEllipse(new SolidBrush(handC), -6, -6, 12, 12);
                }
            }
            g.Restore(state);

            // Полоса с кружками попаданий
            int alpha = (int)Math.Min(penaltyFadeAlpha, barFadeInAlpha);
            if (alpha > 0)
            {
                int barWidth = 320;
                int barHeight = 60;
                int barX = cx - barWidth / 2;
                int barY = cy + (int)outerRadius + 80;

                RectangleF barRect = new RectangleF(barX, barY, barWidth, barHeight);
                using (GraphicsPath barPath = GetRoundedRectPath(barRect, 15f))
                {
                    g.FillPath(new SolidBrush(Color.FromArgb((int)(alpha * (30f / 255f)), 30, 30, 30)), barPath);
                    g.DrawPath(new Pen(Color.FromArgb(alpha, Color.Gray), 3f), barPath);
                }

                // Рисуем цветные кружки для каждого попадания
                for (int i = 0; i < hitColors.Count; i++)
                {
                    bool isLastCircle = (i == hitColors.Count - 1 && !isPenaltyState);
                    float scale = isLastCircle ? hitCircleAnimScale : 1.0f;

                    var sC = g.Save();
                    g.TranslateTransform(barX + 30 + (i * 55) + 20, barY + 30);
                    g.ScaleTransform(scale, scale);

                    using (Brush b = new SolidBrush(Color.FromArgb(alpha, hitColors[i])))
                        g.FillEllipse(b, -20, -20, 40, 40);

                    g.DrawEllipse(new Pen(Color.FromArgb(alpha, Color.White), 3f), -20, -20, 40, 40);

                    // Вспышка вокруг последнего кружка
                    if (isLastCircle && flashAlpha > 0f)
                    {
                        int fA = (int)(flashAlpha * (alpha / 255f));
                        if (fA > 0)
                            using (Brush fb = new SolidBrush(Color.FromArgb(fA, Color.White)))
                                g.FillEllipse(fb, -20, -20, 40, 40);
                    }
                    g.Restore(sC);
                }

                // Рисуем текст комбо с анимацией масштаба
                if (combo > 0)
                {
                    Color baseC = combo >= 20 ? Color.Black : (combo >= 10 ? Color.Red : (combo >= 5 ? Color.Orange : Color.LimeGreen));
                    float baseSizeScale = combo >= 20 ? 1.45f : (combo >= 10 ? 1.30f : (combo >= 5 ? 1.15f : 1.0f));

                    var sCombo = g.Save();
                    g.TranslateTransform(barX + barWidth + 25, barY + barHeight / 2);
                    float totalScale = baseSizeScale * comboAnimScale;
                    g.ScaleTransform(totalScale, totalScale);

                    using (GraphicsPath path = new GraphicsPath())
                    using (FontFamily ff = new FontFamily("Consolas"))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                        path.AddString($"x{combo}", ff, (int)FontStyle.Bold, 48f, new Point(0, 0), sf);

                        using (Pen p = new Pen(Color.FromArgb(alpha, Color.White), 3f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, path); }
                        using (Brush b = new SolidBrush(Color.FromArgb(alpha, baseC))) g.FillPath(b, path);

                        // Вспышка на тексте комбо
                        if (!isPenaltyState && flashAlpha > 0f)
                        {
                            int fA = (int)(flashAlpha * (alpha / 255f));
                            if (fA > 0)
                                using (Brush fb = new SolidBrush(Color.FromArgb(fA, Color.White)))
                                    g.FillPath(fb, path);
                        }
                    }
                    g.Restore(sCombo);
                }
            }

            // Подсказка в начале игры
            if (!isGameStarted && !isPenaltyState)
            {
                using (Font f = new Font("Consolas", 14, FontStyle.Italic))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Press SPACE to start", f, Brushes.Gray, new Rectangle(0, cy + (int)outerRadius + 150, this.Width, 50), sf);
                }
            }
        }
    }
}