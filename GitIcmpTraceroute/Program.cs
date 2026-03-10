using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace IcmpTraceroute
{
    class Program
    {
        struct IcmpHeader
        {
            public byte type;
            public byte code;
            public ushort checksum;
            public ushort id;
            public ushort sequence;
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Использование: IcmpTraceroute.exe <IP-адрес или домен>");
                return;
            }

            string host = args[0];
            int maxHops = 30;
            int packetsPerHop = 3;
            int baseId = 12345;

            IPAddress destination;
            try
            {
                destination = Dns.GetHostEntry(host).AddressList[0];
                Console.WriteLine($"Трассировка к {host} [{destination}] с макс. {maxHops} прыжков...\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка разрешения имени: {ex.Message}");
                return;
            }

            Socket icmpSocket = null;
            try
            {
                icmpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
                icmpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                icmpSocket.ReceiveTimeout = 6000;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось создать ICMP-сокет. Возможно, требуется запуск от администратора.\n{ex.Message}");
                return;
            }

            byte[] receiveBuffer = new byte[4096];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                Console.Write($"{ttl,2}: ");

                for (int attempt = 0; attempt < packetsPerHop; attempt++)
                {
                    icmpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);

                    ushort id = (ushort)(baseId + attempt);
                    ushort seq = (ushort)(ttl * 10 + attempt);

                    byte[] icmpData = CreateIcmpEchoRequest(id, seq, "test");
                    long sentTicks = DateTime.Now.Ticks;

                    try
                    {
                        icmpSocket.SendTo(icmpData, new IPEndPoint(destination, 0));

                        bool received = false;
                        int timeout = 6000;
                        int startTick = Environment.TickCount;

                        while (!received && (Environment.TickCount - startTick) < timeout)
                        {
                            if (icmpSocket.Poll(500000, SelectMode.SelectRead))
                            {
                                int receivedBytes = icmpSocket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
                                long receivedTicks = DateTime.Now.Ticks;

                                IPAddress responderAddr = ((IPEndPoint)remoteEndPoint).Address;
                                int ipHeaderLen = (receiveBuffer[0] & 0x0F) * 4;
                                int icmpOffset = ipHeaderLen;
                                byte icmpType = receiveBuffer[icmpOffset];
                                byte icmpCode = receiveBuffer[icmpOffset + 1];

                                // Для отладки можно раскомментировать:
                                // Console.WriteLine($"\n[DEBUG] Получен ICMP тип={icmpType} от {responderAddr}");

                                if (icmpType == 11) // Time Exceeded – любой подходит
                                {
                                    long rtt = (receivedTicks - sentTicks) / TimeSpan.TicksPerMillisecond;
                                    Console.Write($"  {responderAddr} ({rtt} ms)");
                                    received = true;
                                }
                                else if (icmpType == 0) // Echo Reply – проверяем, что это наш
                                {
                                    if (IsOurPacket(receiveBuffer, icmpOffset, id, seq))
                                    {
                                        long rtt = (receivedTicks - sentTicks) / TimeSpan.TicksPerMillisecond;
                                        Console.Write($"  {responderAddr} ({rtt} ms)");
                                        Console.WriteLine("\nДостигнут конечный узел.");
                                        icmpSocket.Close();
                                        return;
                                    }
                                }
                            }
                        }

                        if (!received)
                        {
                            Console.Write($"  *");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write($"  [ERR]");
                    }
                }
                Console.WriteLine();
                Thread.Sleep(100);
            }

            icmpSocket.Close();
            Console.WriteLine("Трассировка завершена.");
        }

        static byte[] CreateIcmpEchoRequest(ushort id, ushort seq, string data)
        {
            byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(data);
            byte[] packet = new byte[8 + dataBytes.Length];

            packet[0] = 8;
            packet[1] = 0;
            packet[2] = 0;
            packet[3] = 0;
            packet[4] = (byte)(id >> 8);
            packet[5] = (byte)(id & 0xFF);
            packet[6] = (byte)(seq >> 8);
            packet[7] = (byte)(seq & 0xFF);

            Array.Copy(dataBytes, 0, packet, 8, dataBytes.Length);

            ushort checksum = ComputeChecksum(packet);
            packet[2] = (byte)(checksum >> 8);
            packet[3] = (byte)(checksum & 0xFF);

            return packet;
        }

        static ushort ComputeChecksum(byte[] data)
        {
            long sum = 0;
            int i = 0;
            while (i < data.Length - 1)
            {
                sum += (data[i++] << 8) | data[i++];
            }
            if (i < data.Length)
            {
                sum += (data[i] << 8);
            }
            sum = (sum >> 16) + (sum & 0xFFFF);
            sum += (sum >> 16);
            return (ushort)(~sum);
        }

        static bool IsOurPacket(byte[] receivedPacket, int icmpOffset, ushort expectedId, ushort expectedSeq)
        {
            if (receivedPacket.Length < icmpOffset + 8) return false;
            ushort id = (ushort)((receivedPacket[icmpOffset + 4] << 8) | receivedPacket[icmpOffset + 5]);
            ushort seq = (ushort)((receivedPacket[icmpOffset + 6] << 8) | receivedPacket[icmpOffset + 7]);
            return id == expectedId && seq == expectedSeq;
        }
    }
}