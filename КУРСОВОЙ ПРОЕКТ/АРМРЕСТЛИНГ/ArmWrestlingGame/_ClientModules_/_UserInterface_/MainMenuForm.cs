using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArmWrestlingGame
{
    public partial class MainMenuForm : Form
    {
        private Timer bgTimer;
        private float bgOffset = 0f;

        private string currentUser;
        private Form loginFormRef;

        /// <summary>
        /// Инициализирует главное меню с информацией о текущем пользователе.
        /// Устанавливает полноэкранный режим без рамки и анимированный фон.
        /// </summary>
        public MainMenuForm(string username, Form loginForm)
        {
            currentUser = username;
            loginFormRef = loginForm;

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(10, 10, 10);

            InitializeMenuUI();

            bgTimer = new Timer { Interval = 30 };
            bgTimer.Tick += (s, e) => { bgOffset += 0.5f; Invalidate(); };

            this.VisibleChanged += (s, e) => { if (this.Visible) bgTimer.Start(); else bgTimer.Stop(); };
        }

        /// <summary>
        /// Создаёт и размещает UI элементы меню: заголовок, кнопки режимов игры и управления.
        /// </summary>
        private void InitializeMenuUI()
        {
            int cx = Screen.PrimaryScreen.Bounds.Width / 2;
            int cy = Screen.PrimaryScreen.Bounds.Height / 2;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            // Отображение текущего пользователя в левом верхнем углу
            Label lblUser = new Label
            {
                Text = $"User: {currentUser}",
                Font = new Font("Consolas", 16, FontStyle.Bold),
                ForeColor = Color.LimeGreen,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(20, 20)
            };
            this.Controls.Add(lblUser);

            // Заголовок приложения в центре
            Label lblTitle = new Label
            {
                Text = "ARMWRESTLING",
                Font = new Font("Consolas", 72, FontStyle.Bold | FontStyle.Italic),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            this.Controls.Add(lblTitle);
            lblTitle.Location = new Point(cx - lblTitle.PreferredWidth / 2, cy - 250);

            // Кнопка одиночной игры
            Button btn1P = CreateMenuButton("1 Player", cx - 280, cy - 50, 240, 80, 24);
            btn1P.Click += Btn1P_Click;
            this.Controls.Add(btn1P);

            // Кнопка игры на двоих (по сети)
            Button btn2P = CreateMenuButton("2 Players", cx + 40, cy - 50, 240, 80, 24);
            btn2P.Enabled = true;
            btn2P.Click += Btn2P_Click;
            this.Controls.Add(btn2P);

            // Кнопка выхода в меню входа
            Button btnLogOff = CreateMenuButton("Log Off", cx - 100, screenHeight - 240, 200, 60, 18);
            btnLogOff.Click += (s, e) =>
            {
                SoundManager.PlayClick();
                LoginForm.ReleaseSessionLock();
                loginFormRef.Show();
                this.Close();
            };
            this.Controls.Add(btnLogOff);

            // Кнопка выхода из приложения
            Button btnExit = CreateMenuButton("Exit", cx - 100, screenHeight - 140, 200, 60, 18);
            btnExit.Click += (s, e) => { 
                SoundManager.PlayClick();
                LoginForm.ReleaseSessionLock();
                Application.Exit(); };
            this.Controls.Add(btnExit);
        }

        /// <summary>
        /// Создаёт кнопку меню с заданными параметрами и эффектом наведения мыши.
        /// </summary>
        private Button CreateMenuButton(string text, int x, int y, int w, int h, int fontSize)
        {
            Button btn = new Button
            {
                Text = text,
                Font = new Font("Consolas", fontSize, FontStyle.Bold),
                Size = new Size(w, h),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(20, 20, 20),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 3;
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(50, 50, 50);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(20, 20, 20);
            return btn;
        }

        /// <summary>
        /// Открывает форму одиночной игры и скрывает меню.
        /// </summary>
        private void Btn1P_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            SinglePlayerForm spForm = new SinglePlayerForm(currentUser, this);
            spForm.Show();
            this.Hide();
        }

        /// <summary>
        /// Запрашивает IP сервера и открывает форму двухигровой игры по сети.
        /// </summary>
        private void Btn2P_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();

            string ip = PromptForIP();

            if (string.IsNullOrWhiteSpace(ip)) return;

            TwoPlayerForm tpForm = new TwoPlayerForm(currentUser, this, ip);
            tpForm.Show();
            this.Hide();
        }

        /// <summary>
        /// Рисует анимированный диагональный фон с сеткой.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen gridPen = new Pen(Color.FromArgb(18, 18, 18), 2f))
            {
                for (float i = -this.Width; i < this.Width * 2; i += 100)
                {
                    g.DrawLine(gridPen, i + bgOffset, 0, i - this.Height + bgOffset, this.Height);
                    g.DrawLine(gridPen, i - bgOffset, 0, i + this.Height - bgOffset, this.Height);
                }
            }
        }

        /// <summary>
        /// Показывает диалоговое окно для ввода IP адреса сервера.
        /// </summary>
        private string PromptForIP()
        {
            Form prompt = new Form()
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Enter Server IP",
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label textLabel = new Label() { Left = 20, Top = 20, Text = "IP Address:" };
            TextBox textBox = new TextBox() { Left = 20, Top = 45, Width = 240, Text = "127.0.0.1" };
            Button confirmation = new Button() { Text = "Connect", Left = 160, Width = 100, Top = 75, DialogResult = DialogResult.OK };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
        }
    }
}