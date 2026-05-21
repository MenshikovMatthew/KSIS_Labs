using System;
using System.Windows.Forms;

namespace ArmWrestlingGame
{
    internal static class Program
    {
        /// <summary>
        /// Точка входа приложения. Инициализирует менеджеры и запускает главную форму авторизации.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            SoundManager.Init();
            AuthManager.Init(); // Инициализация папок и файлов

            Application.Run(new LoginForm());
        }
    }
}