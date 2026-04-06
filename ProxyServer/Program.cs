using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class ProxyServer
{
    private readonly int _port;

    public ProxyServer(int port) => _port = port;

    public async Task StartAsync()
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"Прокси-сервер запущен на порту {_port} (адрес: 0.0.0.0)");
        Console.WriteLine("Настройте браузер на прокси 127.0.0.2:8888\n");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var clientStream = client.GetStream())
        {
            try
            {
                byte[] buffer = new byte[8192];
                int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) return;

                // Ищем позицию "\r\n\r\n" – конец HTTP-заголовков
                int headerEnd = FindHeaderEnd(buffer, bytesRead);
                if (headerEnd == -1) return;

                string requestString = Encoding.ASCII.GetString(buffer, 0, headerEnd);
                string[] lines = requestString.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0) return;

                string[] requestLine = lines[0].Split(' ');
                if (requestLine.Length < 3) return;

                string method = requestLine[0];
                string fullUri = requestLine[1];
                string httpVersion = requestLine[2];

                // HTTPS использует CONNECT – такие запросы пропускаем
                if (method == "CONNECT") return;

                // Преобразуем запрошенный URI в объект Uri (поддержка как полного URL, так и пути + Host)
                Uri uri = null;
                if (fullUri.StartsWith("http://"))
                {
                    if (!Uri.TryCreate(fullUri, UriKind.Absolute, out uri)) return;
                }
                else
                {
                    string host = null;
                    foreach (var line in lines)
                        if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                        {
                            host = line.Substring(5).Trim();
                            break;
                        }
                    if (string.IsNullOrEmpty(host)) return;
                    string fullUrl = "http://" + host + fullUri;
                    if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out uri)) return;
                }

                string targetHost = uri.Host;
                int targetPort = uri.Port == -1 ? 80 : uri.Port;
                string targetPath = uri.PathAndQuery;

                // Формируем новый запрос: заменяем полный URL на путь
                string newFirstLine = $"{method} {targetPath} {httpVersion}";
                string modifiedRequest = newFirstLine + "\r\n";
                for (int i = 1; i < lines.Length; i++)
                    modifiedRequest += lines[i] + "\r\n";
                modifiedRequest += "\r\n";

                byte[] modifiedRequestBytes = Encoding.ASCII.GetBytes(modifiedRequest);

                // Тело запроса (если есть, например, при POST)
                int bodyLength = bytesRead - headerEnd;
                byte[] body = null;
                if (bodyLength > 0)
                {
                    body = new byte[bodyLength];
                    Array.Copy(buffer, headerEnd, body, 0, bodyLength);
                }

                using (var target = new TcpClient())
                {
                    try
                    {
                        await target.ConnectAsync(targetHost, targetPort);
                    }
                    catch
                    {
                        // Не удалось подключиться – просто выходим (журнал не требуется)
                        return;
                    }

                    using (var targetStream = target.GetStream())
                    {
                        await targetStream.WriteAsync(modifiedRequestBytes, 0, modifiedRequestBytes.Length);
                        if (body != null) await targetStream.WriteAsync(body, 0, body.Length);

                        // Читаем ответ сервера до конца заголовков
                        var responseBuffer = new MemoryStream();
                        byte[] respBuffer = new byte[8192];
                        bool headersComplete = false;
                        int respHeaderEnd = -1;

                        while (!headersComplete)
                        {
                            int read = await targetStream.ReadAsync(respBuffer, 0, respBuffer.Length);
                            if (read == 0) break;
                            responseBuffer.Write(respBuffer, 0, read);
                            byte[] data = responseBuffer.ToArray();
                            for (int i = 0; i < data.Length - 3; i++)
                            {
                                if (data[i] == '\r' && data[i + 1] == '\n' &&
                                    data[i + 2] == '\r' && data[i + 3] == '\n')
                                {
                                    headersComplete = true;
                                    respHeaderEnd = i + 4;
                                    break;
                                }
                            }
                        }

                        if (!headersComplete) return;

                        // Извлекаем код статуса из первой строки ответа
                        byte[] headerBytes = new byte[respHeaderEnd];
                        Array.Copy(responseBuffer.ToArray(), 0, headerBytes, 0, respHeaderEnd);
                        string responseHeaders = Encoding.ASCII.GetString(headerBytes);
                        string[] responseLines = responseHeaders.Split(new[] { "\r\n" }, StringSplitOptions.None);
                        string statusCode = "???";
                        if (responseLines.Length > 0)
                        {
                            var parts = responseLines[0].Split(' ');
                            if (parts.Length >= 3) statusCode = parts[1];
                        }

                        // Выводим журнал: URL и код ответа
                        Console.WriteLine($"{fullUri} - {statusCode}");

                        // Пересылаем заголовки клиенту
                        await clientStream.WriteAsync(headerBytes, 0, headerBytes.Length);

                        // Отправляем уже прочитанную часть тела
                        int alreadyReadBody = (int)responseBuffer.Length - respHeaderEnd;
                        if (alreadyReadBody > 0)
                        {
                            byte[] bodyPart = new byte[alreadyReadBody];
                            Array.Copy(responseBuffer.ToArray(), respHeaderEnd, bodyPart, 0, alreadyReadBody);
                            await clientStream.WriteAsync(bodyPart, 0, bodyPart.Length);
                        }

                        // Пересылаем остальные данные (необходимо для потокового радио)
                        while (true)
                        {
                            int read = await targetStream.ReadAsync(respBuffer, 0, respBuffer.Length);
                            if (read == 0) break;
                            await clientStream.WriteAsync(respBuffer, 0, read);
                        }
                    }
                }
            }
            catch { }
        }
    }

    // Поиск последовательности \r\n\r\n в буфере
    private int FindHeaderEnd(byte[] buffer, int length)
    {
        for (int i = 0; i < length - 3; i++)
            if (buffer[i] == '\r' && buffer[i + 1] == '\n' &&
                buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                return i + 4;
        return -1;
    }
}

class Program
{
    static async Task Main()
    {
        var proxy = new ProxyServer(8888);
        await proxy.StartAsync();
    }
}