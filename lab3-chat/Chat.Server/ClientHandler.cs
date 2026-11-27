using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Chat.Common;

namespace Chat.Server;

/// <summary>
/// Обработчик одного клиента. Для каждого подключения сервер запускает отдельный поток
/// с методом <see cref="Run"/>: он в цикле читает строки от клиента (чтение блокирует поток,
/// пока ничего не пришло), поэтому один «молчащий» клиент не мешает остальным.
/// </summary>
public class ClientHandler
{
    private const int LoginTimeoutMs = 10_000;

    private readonly TcpClient _tcp;
    private readonly ChatServer _server;
    private readonly NetworkStream _stream;
    private readonly object _writeLock = new(); // два потока не должны писать в сокет одновременно
    private int _closed;

    /// <summary>Ник клиента; пустой, пока он не вошёл.</summary>
    public string? Nick { get; internal set; }

    /// <summary>Адрес клиента для журнала сервера.</summary>
    public string Address { get; }

    public ClientHandler(TcpClient tcp, ChatServer server)
    {
        _tcp = tcp;
        _server = server;
        _tcp.NoDelay = true;
        _tcp.SendTimeout = 5000; // если клиент завис и не читает, не ждём вечно
        _stream = tcp.GetStream();
        Address = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
    }

    /// <summary>Работа потока клиента: вход, затем чтение сообщений до отключения.</summary>
    public void Run()
    {
        try
        {
            using var reader = new StreamReader(_stream, new UTF8Encoding(false));

            // Первое сообщение должно прийти быстро и быть «join». Иначе отключаем.
            _tcp.ReceiveTimeout = LoginTimeoutMs;
            string? first = reader.ReadLine();
            if (first == null || !Login(first)) return;
            _tcp.ReceiveTimeout = 0; // дальше ждём сколько угодно

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Handle(line);
            }
        }
        catch (IOException) { }                 // клиент закрыл окно или пропала сеть
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            // Что бы ни случилось, сервер продолжает работать: убираем клиента и закрываем сокет.
            _server.Leave(this);
            Close();
        }
    }

    /// <summary>Проверка первого сообщения. true — клиент вошёл.</summary>
    private bool Login(string line)
    {
        ChatMessage join;
        try
        {
            join = ChatMessage.FromJson(line);
        }
        catch (JsonException)
        {
            Send(ChatMessage.Create(MessageTypes.Error, "", "Первое сообщение должно быть «join»."));
            return false;
        }

        string nick = (join.Nick ?? "").Trim();
        if (join.Type != MessageTypes.Join)
        {
            Send(ChatMessage.Create(MessageTypes.Error, "", "Сначала нужно войти (сообщение «join»)."));
            return false;
        }
        if (nick.Length == 0 || nick.Length > ChatDefaults.MaxNickLength || ContainsControl(nick))
        {
            Send(ChatMessage.Create(MessageTypes.Error, "",
                $"Ник должен быть от 1 до {ChatDefaults.MaxNickLength} символов."));
            return false;
        }

        if (!_server.TryJoin(this, nick, out string error))
        {
            Send(ChatMessage.Create(MessageTypes.Error, "", error));
            return false;
        }

        _server.AnnounceJoin(this);
        return true;
    }

    private static bool ContainsControl(string text)
    {
        foreach (char c in text)
            if (char.IsControl(c)) return true;
        return false;
    }

    /// <summary>Разбор сообщения уже вошедшего клиента.</summary>
    private void Handle(string line)
    {
        ChatMessage message;
        try
        {
            message = ChatMessage.FromJson(line);
        }
        catch (JsonException)
        {
            Send(ChatMessage.Create(MessageTypes.Error, "", "Не удалось разобрать сообщение."));
            return;
        }

        string text = (message.Text ?? "").Trim();
        if (text.Length == 0) return;
        if (text.Length > ChatDefaults.MaxTextLength) text = text.Substring(0, ChatDefaults.MaxTextLength);

        switch (message.Type)
        {
            case MessageTypes.Text:
                _server.BroadcastText(this, text);
                break;
            case MessageTypes.Private:
                _server.SendPrivate(this, message.To ?? "", text);
                break;
            default:
                Send(ChatMessage.Create(MessageTypes.Error, "", $"Неизвестный тип сообщения: {message.Type}"));
                break;
        }
    }

    /// <summary>
    /// Отправить сообщение клиенту (одна строка JSON + перевод строки).
    /// Вызывается из потоков других клиентов, поэтому запись под своим lock.
    /// При ошибке записи закрывает соединение, а поток клиента сам уберёт его из списка.
    /// </summary>
    public bool Send(ChatMessage message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message.ToJson() + "\n");
        lock (_writeLock)
        {
            try
            {
                _stream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
        Close();
        return false;
    }

    /// <summary>Закрыть соединение (можно вызывать много раз).</summary>
    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1) return;
        try { _tcp.Close(); } catch (SocketException) { }
    }
}
