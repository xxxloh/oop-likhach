using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Chat.Common;

/// <summary>
/// Одно сообщение протокола. Умеет превращаться в строку JSON и обратно.
/// Общий класс для сервера и клиента: оба понимают друг друга, потому что используют один и тот же формат.
/// </summary>
/// <param name="Type">Вид сообщения, см. <see cref="MessageTypes"/>.</param>
/// <param name="Nick">Ник отправителя (для служебных сообщений может быть пустым).</param>
/// <param name="Text">Текст сообщения.</param>
/// <param name="Time">Время отправки по UTC (клиент показывает его в местном времени).</param>
/// <param name="To">Ник получателя, только для личных сообщений.</param>
/// <param name="Users">Список ников онлайн, только для сообщения типа «users».</param>
public record ChatMessage(
    string Type,
    string Nick,
    string Text,
    DateTime Time,
    string? To,
    List<string>? Users)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Объект → строка JSON (одна строка, без переводов строки внутри).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Строка JSON → объект. При битом JSON бросает <see cref="JsonException"/>.</summary>
    public static ChatMessage FromJson(string line) =>
        JsonSerializer.Deserialize<ChatMessage>(line, Options)
        ?? throw new JsonException("Пустое сообщение");

    /// <summary>Короткий конструктор для сообщений без получателя и списка.</summary>
    public static ChatMessage Create(string type, string nick, string text) =>
        new(type, nick, text, DateTime.UtcNow, null, null);
}
