using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ArmWrestlingGame
{
    public static class SoundManager
    {
        [DllImport("winmm.dll")]
        private static extern long mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr winHandle);

        private static string soundsPath;
        private static HashSet<string> loadedAliases = new HashSet<string>();

        /// <summary>
        /// Инициализирует путь к папке со звуками.
        /// </summary>
        public static void Init()
        {
            soundsPath = Path.Combine(Application.StartupPath, "Sounds");
        }

        // Методы для проигрывания различных звуков игры
        public static void PlayClick() => PlaySound("click.wav", "click");
        public static void PlayHit() => PlaySound("hit.wav", "hit");
        public static void PlayMiss() => PlaySound("miss.wav", "miss");
        public static void PlayVictory() => PlaySound("victory.wav", "victory");
        public static void PlayDefeat() => PlaySound("defeat.wav", "defeat");
        public static void PlayDraw() => PlaySound("draw.wav", "draw");
        public static void PlayCountdown() => PlaySound("countdown.wav", "countdown");
        public static void PlayFound() => PlaySound("found.wav", "found");

        /// <summary>
        /// Загружает звук один раз в кэш и воспроизводит его с начала.
        /// Использует MCI команды для эффективного управления звуками.
        /// </summary>
        private static void PlaySound(string fileName, string alias)
        {
            string fullPath = Path.Combine(soundsPath, fileName);
            if (File.Exists(fullPath))
            {
                // Загружаем звук в память только при первом воспроизведении
                if (!loadedAliases.Contains(alias))
                {
                    mciSendString($"open \"{fullPath}\" type waveaudio alias {alias}", null, 0, IntPtr.Zero);
                    loadedAliases.Add(alias);
                }

                // Воспроизводим звук с начала
                mciSendString($"play {alias} from 0", null, 0, IntPtr.Zero);
            }
        }
    }
}