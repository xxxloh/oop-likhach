using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Chat.Common;

namespace Chat.Server;

internal static class Program
{
    public static int Main(string[] args)
    {
        int port = ChatDefaults.DefaultPort;
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out port) || port < 1 || port > 65535)
            {
                Console.WriteLine("Порт должен быть числом от 1 до 65535. Пример: dotnet run -- 5000");
                return 1;
            }
        }

        // История лежит в файле рядом с тем местом, откуда запущен сервер.
        string historyPath = Path.GetFullPath("history.jsonl");
        var server = new ChatServer(port, new MessageHistory(historyPath));

        // Ctrl+C: аккуратно останавливаем сервер, а не обрываем процесс.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Stop();
        };

        PrintAddresses(port);
        Console.WriteLine($"История сообщений: {historyPath}");
        Console.WriteLine("Остановить сервер: Ctrl+C");

        try
        {
            server.Run();
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Не удалось запустить сервер на порту {port}: {ex.Message}");
            Console.WriteLine("Возможно, порт занят другой программой (например, вторым сервером). Попробуйте другой порт.");
            return 1;
        }

        Console.WriteLine("Сервер остановлен.");
        return 0;
    }

    /// <summary>Печатает адреса, по которым к серверу можно подключиться с другого компьютера.</summary>
    private static void PrintAddresses(int port)
    {
        Console.WriteLine($"Порт: {port}");
        Console.WriteLine("Адрес для подключения с этого же компьютера: 127.0.0.1");
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var info in nic.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (System.Net.IPAddress.IsLoopback(info.Address)) continue;
                    Console.WriteLine($"Адрес для подключения с другого компьютера в сети: {info.Address}");
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Адреса — просто подсказка, без них сервер работает.
        }
    }
}
