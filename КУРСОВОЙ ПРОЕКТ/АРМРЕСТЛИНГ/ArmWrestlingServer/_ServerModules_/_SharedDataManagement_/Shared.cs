using System;

namespace ArmWrestlingServer
{
    // Типы пакетов для синхронизации между клиентом и сервером
    public enum PacketType
    {
        Matchmake,      // Запрос поиска матча
        Found,          // Противник найден
        SyncStart,      // Синхронизация начала игры
        HitEvent,       // Событие удара
        StateUpdate,    // Обновление состояния (комбо, наклон)
        GameEnd,        // Завершение игры
        RematchRequest, // Запрос реванша
        RematchAccept,  // Принятие реванша
        RematchDeny     // Отказ от реванша
    }

    // Структура пакета для передачи данных между клиентом и сервером
    public class GamePacket
    {
        // Тип события пакета
        public PacketType Type { get; set; }
        // Полезная нагрузка в виде JSON строки
        public string Payload { get; set; }
    }
}