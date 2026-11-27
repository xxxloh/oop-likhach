using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chat.Common;

namespace Chat.Client;

/// <summary>Сервер не пустил в чат (например, ник занят). В Message — причина для пользователя.</summary>
public class LoginRejectedException : Exception
{
    public LoginRejectedException(string message) : base(message) { }
}

/// <summary>
/// Сетевая часть клиента. Ничего не знает про окна и Avalonia: только соединение и события.
/// Окно подписывается на <see cref="MessageReceived"/> (паттерн Наблюдатель / Observer):
/// клиент сообщает «пришло сообщение», а что с ним делать — решает подписчик.
/// Чтение и запись идут в своих фоновых потоках, поэтому окно не зависает.
/// </summary>
public class ChatClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly BlockingCollection<string> _outgoing = new(); // очередь на отправку
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private int _disconnectedRaised;
    private volatile bool _closedByUser;

    /// <summary>Пришло сообщение от сервера. Вызывается из ФОНОВОГО потока.</summary>
    public event Action<ChatMessage>? MessageReceived;

    /// <summary>Соединение потеряно (сервер выключен, пропала сеть). Аргумент — текст для пользователя.
    /// Не вызывается, если пользователь сам вызвал <see cref="Close"/>. Вызывается из фонового потока.</summary>
    public event Action<string>? Disconnected;

    public string Nick { get; private set; } = "";

    /// <summary>
    /// Подключается и входит под ником. Исключения:
    /// <see cref="LoginRejectedException"/> — ник не принят; <see cref="TimeoutException"/> — сервер не ответил;
    /// <see cref="SocketException"/> / <see cref="IOException"/> — не удалось соединиться.
    /// </summary>
    public async Task ConnectAsync(string host, int port, string nick)
    {
        var tcp = new TcpClient { NoDelay = true };
        try
        {
            await tcp.ConnectAsync(host, port).WaitAsync(ConnectTimeout).ConfigureAwait(false);

            NetworkStream stream = tcp.GetStream();
            var reader = new StreamReader(stream, new UTF8Encoding(false));

            // Первое сообщение клиента — «join» с желаемым ником.
            byte[] join = Encoding.UTF8.GetBytes(ChatMessage.Create(MessageTypes.Join, nick, "").ToJson() + "\n");
            await stream.WriteAsync(join, 0, join.Length).WaitAsync(ConnectTimeout).ConfigureAwait(false);

            // Ждём ответ: «ok» или «error».
            string? line = await reader.ReadLineAsync().WaitAsync(ConnectTimeout).ConfigureAwait(false);
            if (line == null) throw new IOException("Сервер закрыл соединение.");

            ChatMessage reply = ChatMessage.FromJson(line);
            if (reply.Type == MessageTypes.Error) throw new LoginRejectedException(reply.Text);
            if (reply.Type != MessageTypes.Ok) throw new IOException("Неожиданный ответ сервера.");

            tcp.SendTimeout = 5000;
            _tcp = tcp;
            _stream = stream;
            _reader = reader;
            Nick = nick;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>Запустить фоновые потоки чтения и отправки. Вызывать после подписки на события.</summary>
    public void StartReading()
    {
        new Thread(ReadLoop) { IsBackground = true }.Start();
        new Thread(WriteLoop) { IsBackground = true }.Start();
    }

    /// <summary>Сообщение всем.</summary>
    public void SendText(string text) =>
        Enqueue(new ChatMessage(MessageTypes.Text, Nick, text, DateTime.UtcNow, null, null));

    /// <summary>Личное сообщение (★).</summary>
    public void SendPrivate(string toNick, string text) =>
        Enqueue(new ChatMessage(MessageTypes.Private, Nick, text, DateTime.UtcNow, toNick, null));

    /// <summary>Положить сообщение в очередь; реальную отправку сделает фоновый поток (окно не ждёт сеть).</summary>
    private void Enqueue(ChatMessage message)
    {
        try
        {
            _outgoing.Add(message.ToJson() + "\n");
        }
        catch (InvalidOperationException)
        {
            // Уже закрыто — отправлять некуда.
        }
    }

    /// <summary>Фоновый поток: читает строки от сервера и сообщает подписчикам.</summary>
    private void ReadLoop()
    {
        try
        {
            string? line;
            while ((line = _reader!.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                ChatMessage message;
                try
                {
                    message = ChatMessage.FromJson(line);
                }
                catch (JsonException)
                {
                    continue; // непонятную строку пропускаем, чат продолжает работать
                }
                MessageReceived?.Invoke(message);
            }
        }
        catch (IOException) { }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }

        OnConnectionLost();
    }

    /// <summary>Фоновый поток: отправляет сообщения из очереди по одному, в том же порядке.</summary>
    private void WriteLoop()
    {
        try
        {
            foreach (string line in _outgoing.GetConsumingEnumerable())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                _stream!.Write(bytes, 0, bytes.Length);
            }
        }
        catch (IOException) { OnConnectionLost(); }
        catch (SocketException) { OnConnectionLost(); }
        catch (ObjectDisposedException) { OnConnectionLost(); }
    }

    /// <summary>Сообщает об обрыве один раз, даже если сработали оба потока.</summary>
    private void OnConnectionLost()
    {
        if (_closedByUser) return;
        if (Interlocked.Exchange(ref _disconnectedRaised, 1) == 1) return;

        CloseSocket();
        Disconnected?.Invoke("Соединение с сервером потеряно.");
    }

    /// <summary>Пользователь сам выходит из чата.</summary>
    public void Close()
    {
        _closedByUser = true;
        _outgoing.CompleteAdding();
        CloseSocket();
    }

    private void CloseSocket()
    {
        try { _tcp?.Client.Shutdown(SocketShutdown.Both); } catch (SocketException) { } catch (ObjectDisposedException) { }
        try { _tcp?.Close(); } catch (SocketException) { }
    }
}
