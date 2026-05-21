using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace ArmWrestlingGame
{
    public static class AuthManager
    {
        private static string loginsPath;
        private static string usersFile;
        private static string passwordsFile;

        public static string InfoPath { get; private set; }

        /// <summary>
        /// Инициализирует пути к папкам и файлам для хранения учётных данных и статистики.
        /// Создаёт необходимые директории и файлы, если они отсутствуют.
        /// </summary>
        public static void Init()
        {
            loginsPath = Path.Combine(Application.StartupPath, "Logins");
            InfoPath = Path.Combine(Application.StartupPath, "Info");

            usersFile = Path.Combine(loginsPath, "users.json");
            passwordsFile = Path.Combine(loginsPath, "passwords.json");

            if (!Directory.Exists(loginsPath)) Directory.CreateDirectory(loginsPath);
            if (!Directory.Exists(InfoPath)) Directory.CreateDirectory(InfoPath);

            if (!File.Exists(usersFile)) File.WriteAllText(usersFile, "[]");
            if (!File.Exists(passwordsFile)) File.WriteAllText(passwordsFile, "{}");
        }

        /// <summary>
        /// Регистрирует новый пользователь с проверкой на занятость имени пользователя.
        /// Сохраняет пароль в виде хеша SHA256.
        /// </summary>
        /// <returns>true если регистрация успешна, false если пользователь уже существует или данные пусты</returns>
        public static bool SignUp(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return false;

            var users = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(usersFile)) ?? new List<string>();
            var passwords = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(passwordsFile)) ?? new Dictionary<string, string>();

            if (users.Contains(username)) return false; // Имя уже занято

            users.Add(username);
            passwords[username] = HashPassword(password);

            File.WriteAllText(usersFile, JsonSerializer.Serialize(users));
            File.WriteAllText(passwordsFile, JsonSerializer.Serialize(passwords));

            return true;
        }

        /// <summary>
        /// Проверяет учётные данные пользователя при входе.
        /// </summary>
        /// <returns>true если пароль совпадает с сохранённым хешем, false в противном случае</returns>
        public static bool LogIn(string username, string password)
        {
            var passwords = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(passwordsFile)) ?? new Dictionary<string, string>();

            if (passwords.TryGetValue(username, out string savedHash))
            {
                return savedHash == HashPassword(password);
            }
            return false;
        }

        /// <summary>
        /// Хеширует пароль с использованием алгоритма SHA256.
        /// </summary>
        private static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }

    // Статистика игрока по количеству попаданий, промахов и лучшей комбо
    public class PlayerStats
    {
        public int TotalHits { get; set; } = 0;
        public int TotalMisses { get; set; } = 0;
        public int MaxCombo { get; set; } = 0;
    }
}