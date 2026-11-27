using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Chat.Common;

namespace Chat.Server;

/// <summary>
/// История чата (★★). Каждое сообщение дописывается в файл отдельной строкой JSON,
/// поэтому после перезапуска сервера история не теряется. В памяти хранятся последние 20.
/// Личные сообщения сюда не попадают: их видят только двое.
/// </summary>
public class MessageHistory
{
    private readonly string _path;
    private readonly object _sync = new();
    private readonly List<ChatMessage> _last = new();

    public MessageHistory(string path)
    {
        _path = path;
        Load();
    }

    /// <summary>Читает файл при запуске сервера и запоминает последние сообщения.</summary>
    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            foreach (string line in File.ReadLines(_path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    ChatMessage message = ChatMessage.FromJson(line);
                    if (message.Type == MessageTypes.Text) Remember(message);
                }
                catch (JsonException)
                {
                    // Битую строку пропускаем, остальная история остаётся.
                }
            }
        }
        catch (IOException) { }
        catch (System.UnauthorizedAccessException) { }
    }

    private void Remember(ChatMessage message)
    {
        _last.Add(message);
        if (_last.Count > ChatDefaults.HistorySize) _last.RemoveAt(0);
    }

    /// <summary>Запоминает сообщение и дописывает его в файл.</summary>
    public void Add(ChatMessage message)
    {
        lock (_sync)
        {
            Remember(message);
            try
            {
                File.AppendAllText(_path, message.ToJson() + "\n", new UTF8Encoding(false));
            }
            catch (IOException) { }
            catch (System.UnauthorizedAccessException) { }
        }
    }

    /// <summary>Копия последних сообщений (от старых к новым).</summary>
    public List<ChatMessage> Last()
    {
        lock (_sync)
        {
            return new List<ChatMessage>(_last);
        }
    }
}
