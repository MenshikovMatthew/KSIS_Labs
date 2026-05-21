using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArmWrestlingGame
{
    public partial class LoginForm : Form
    {
        private Timer bgTimer;
        private float bgOffset = 0f;

        private TextBox txtUsername;
        private TextBox txtPassword;
        private static System.IO.FileStream localSessionLock;

        /// <summary>
        /// Инициализирует форму входа в полноэкранный режим без рамки.
        /// Устанавливает анимированный фон и обработчики видимости для оптимизации.
        /// </summary>
        public LoginForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(10, 10, 10);

            InitializeUI();

            bgTimer = new Timer { Interval = 30 };
            bgTimer.Tick += (s, e) => { bgOffset += 0.5f; Invalidate(); };

            this.VisibleChanged += (s, e) => { if (this.Visible) bgTimer.Start(); else bgTimer.Stop(); };
        }

        /// <summary>
        /// Создаёт UI элементы: заголовок, текстовые поля и кнопки, расположенные по центру экрана.
        /// </summary>
        private void InitializeUI()
        {
            int cx = Screen.PrimaryScreen.Bounds.Width / 2;
            int cy = Screen.PrimaryScreen.Bounds.Height / 2;

            Label lblTitle = new Label
            {
                Text = "AUTHORIZATION",
                Font = new Font("Consolas", 48, FontStyle.Bold | FontStyle.Italic),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            this.Controls.Add(lblTitle);
            lblTitle.Location = new Point(cx - 240, cy - 250);

            Label lblUser = new Label { Text = "Username:", ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Consolas", 14, FontStyle.Bold), AutoSize = true, Location = new Point(cx - 150, cy - 130) };
            this.Controls.Add(lblUser);

            txtUsername = CreateTextBox(cx - 150, cy - 100);

            Label lblPass = new Label { Text = "Password:", ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Consolas", 14, FontStyle.Bold), AutoSize = true, Location = new Point(cx - 150, cy - 40) };
            this.Controls.Add(lblPass);

            txtPassword = CreateTextBox(cx - 150, cy - 10);
            txtPassword.UseSystemPasswordChar = true;

            this.Controls.Add(txtUsername);
            this.Controls.Add(txtPassword);

            Button btnLogin = CreateButton("Log In", cx - 150, cy + 60);
            btnLogin.Click += BtnLogin_Click;
            this.Controls.Add(btnLogin);

            Button btnSignUp = CreateButton("Sign Up", cx + 10, cy + 60);
            btnSignUp.Click += BtnSignUp_Click;
            this.Controls.Add(btnSignUp);

            Button btnExit = CreateButton("Exit", cx - 80, Screen.PrimaryScreen.Bounds.Height - 140);
            btnExit.Size = new Size(160, 50);
            btnExit.Click += (s, e) => { SoundManager.PlayClick(); Application.Exit(); };
            this.Controls.Add(btnExit);
        }

        /// <summary>
        /// Создаёт текстовое поле с заданным стилем.
        /// </summary>
        private TextBox CreateTextBox(int x, int y)
        {
            TextBox tb = new TextBox
            {
                Font = new Font("Consolas", 18),
                Location = new Point(x, y),
                Size = new Size(300, 40),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            return tb;
        }

        /// <summary>
        /// Создаёт кнопку с эффектом наведения мыши.
        /// </summary>
        private Button CreateButton(string text, int x, int y)
        {
            Button btn = new Button
            {
                Text = text,
                Font = new Font("Consolas", 16, FontStyle.Bold),
                Size = new Size(140, 50),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(20, 20, 20),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 2;
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(50, 50, 50);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(20, 20, 20);
            return btn;
        }

        /// <summary>
        /// Обрабатывает вход пользователя: проверяет учётные данные и предотвращает двойные сессии.
        /// Создаёт lock-файл для блокировки повторного входа того же пользователя.
        /// </summary>
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            if (AuthManager.LogIn(txtUsername.Text, txtPassword.Text))
            {
                string lockPath = System.IO.Path.Combine(Application.StartupPath, "Logins", txtUsername.Text + ".lock");
                try
                {
                    localSessionLock = new System.IO.FileStream(lockPath, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
                }
                catch (System.IO.IOException)
                {
                    MessageBox.Show("This user is already online!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MainMenuForm menu = new MainMenuForm(txtUsername.Text, this);
                menu.Show();
                this.Hide();

                txtUsername.Clear();
                txtPassword.Clear();
            }
            else
            {
                MessageBox.Show("Invalid username or password!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обрабатывает регистрацию нового пользователя.
        /// </summary>
        private void BtnSignUp_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClick();
            if (AuthManager.SignUp(txtUsername.Text, txtPassword.Text))
            {
                MessageBox.Show("Registration successful! You can now log in.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Registration failed. Username might be taken or fields are empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Рисует анимированный фон с диагональной сеткой.
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
        /// Освобождает файловый lock и удаляет lock-файл при выходе из приложения.
        /// </summary>
        public static void ReleaseSessionLock()
        {
            try
            {
                if (localSessionLock != null)
                {
                    string lockedFilePath = localSessionLock.Name;
                    localSessionLock.Close();
                    localSessionLock.Dispose();
                    localSessionLock = null;

                    if (System.IO.File.Exists(lockedFilePath))
                    {
                        System.IO.File.Delete(lockedFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Session] Error releasing lock file: {ex.Message}");
            }
        }
    }
}