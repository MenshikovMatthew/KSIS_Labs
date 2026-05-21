using System;

namespace ArmWrestlingServer
{
    class Program
    {
        // Точка входа сервера: создаёт и запускает игровой сервер на порту 5000
        static void Main(string[] args)
        {
            var server = new GameServer();
            server.Start(5000);

            // Держит консоль открытой
            Console.ReadLine();
        }
    }
}