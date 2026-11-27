namespace Chat.Common;

/// <summary>Все виды сообщений протокола (поле Type).</summary>
public static class MessageTypes
{
    /// <summary>Клиент → сервер: «хочу войти под этим ником». Первое сообщение клиента.</summary>
    public const string Join = "join";

    /// <summary>Клиент → сервер: сообщение всем. Сервер → клиенты: то же самое, с ником и временем.</summary>
    public const string Text = "text";

    /// <summary>Личное сообщение (★): в поле To ник получателя.</summary>
    public const string Private = "private";

    /// <summary>Сервер → клиент: вход принят.</summary>
    public const string Ok = "ok";

    /// <summary>Сервер → клиент: ошибка (ник занят, получатель не в сети и т.д.). Причина в Text.</summary>
    public const string Error = "error";

    /// <summary>Сервер → клиенты: «кто-то вошёл / вышел».</summary>
    public const string Notice = "system";

    /// <summary>Сервер → клиенты: текущий список ников онлайн (поле Users).</summary>
    public const string Users = "users";

    /// <summary>Сервер → новый клиент: одно из последних сообщений (★★).</summary>
    public const string History = "history";
}

/// <summary>Общие настройки.</summary>
public static class ChatDefaults
{
    public const int DefaultPort = 5050;
    public const int MaxNickLength = 20;
    public const int MaxTextLength = 2000;
    public const int HistorySize = 20;
}
