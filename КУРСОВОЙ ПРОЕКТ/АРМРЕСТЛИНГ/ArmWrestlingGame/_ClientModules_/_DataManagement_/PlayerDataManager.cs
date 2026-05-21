using System.IO;
using System.Text.Json;

namespace ArmWrestlingGame
{
    // Статистика матчей: количество побед, поражений и ничьих
    public class MatchStats
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
    }

    public static class PlayerDataManager
    {
        /// <summary>
        /// Получает или создаёт папку пользователя для сохранения его данных.
        /// </summary>
        /// <returns>Полный путь к папке пользователя</returns>
        public static string GetUserFolder(string username)
        {
            string path = Path.Combine(AuthManager.InfoPath, username);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// Загружает статистику игрока из файла или возвращает новую пустую статистику.
        /// </summary>
        public static PlayerStats LoadStats(string username)
        {
            string path = Path.Combine(GetUserFolder(username), "statistics.json");
            return File.Exists(path) ? JsonSerializer.Deserialize<PlayerStats>(File.ReadAllText(path)) : new PlayerStats();
        }

        /// <summary>
        /// Сохраняет статистику игрока в файл JSON.
        /// </summary>
        public static void SaveStats(string username, PlayerStats stats)
        {
            string path = Path.Combine(GetUserFolder(username), "statistics.json");
            File.WriteAllText(path, JsonSerializer.Serialize(stats));
        }

        /// <summary>
        /// Загружает статистику матчей игрока из файла или возвращает новую пустую статистику.
        /// </summary>
        public static MatchStats LoadMatchStats(string username)
        {
            string path = Path.Combine(GetUserFolder(username), "matches.json");
            return File.Exists(path) ? JsonSerializer.Deserialize<MatchStats>(File.ReadAllText(path)) : new MatchStats();
        }

        /// <summary>
        /// Сохраняет статистику матчей игрока в файл JSON.
        /// </summary>
        public static void SaveMatchStats(string username, MatchStats stats)
        {
            string path = Path.Combine(GetUserFolder(username), "matches.json");
            File.WriteAllText(path, JsonSerializer.Serialize(stats));
        }
    }
}