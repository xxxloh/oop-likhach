using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Chat.Common;

namespace Chat.Client;

/// <summary>
/// Окно чата. Про сеть не знает ничего: только подписывается на события <see cref="ChatClient"/>
/// и рисует то, что пришло. События приходят из фонового потока, а менять элементы окна можно
/// только в потоке интерфейса, поэтому каждый обработчик сразу делает Dispatcher.UIThread.Post(...).
/// </summary>
public partial class ChatWindow : Window
{
    private const int MaxRows = 500; // старые строки убираем, чтобы лента не росла бесконечно

    private static readonly string[] NickColors =
    {
        "#E53935", "#FB8C00", "#43A047", "#00897B", "#1E88E5", "#8E24AA", "#D81B60", "#6D4C41"
    };

    private readonly ChatClient _client;
    private readonly string _myNick;
    private readonly string _server;

    private string? _recipient;       // кому личное сообщение; null — всем
    private bool _updatingUsers;      // true, пока список «онлайн» перестраивается кодом
    private bool _inHistory;          // идёт ли сейчас блок истории
    private bool _connected = true;
    private bool _leaving;

    public ChatWindow(ChatClient client, string host, int port)
    {
        InitializeComponent();

        _client = client;
        _myNick = client.Nick;
        _server = $"{host}:{port}";

        Title = $"Онлайн-чат — {_myNick}";
        TitleText.Text = $"Вы в чате как {_myNick}";
        StatusText.Text = $"Сервер {_server}";

        // Enter в поле ввода отправляет сообщение (Tunnel — раньше, чем поле обработает клавишу сам).
        InputBox.AddHandler(KeyDownEvent, InputBox_KeyDown, RoutingStrategies.Tunnel);

        // Подписка на события клиента (Observer). Только после этого запускаем чтение.
        _client.MessageReceived += OnMessageReceived;
        _client.Disconnected += OnDisconnected;

        Opened += (_, _) => InputBox.Focus();
        Closed += (_, _) =>
        {
            _client.MessageReceived -= OnMessageReceived;
            _client.Disconnected -= OnDisconnected;
            _client.Close();
        };

        _client.StartReading();
    }

    // ---------- события клиента (фоновый поток → поток интерфейса) ----------

    private void OnMessageReceived(ChatMessage message) =>
        Dispatcher.UIThread.Post(() => ShowMessage(message));

    private void OnDisconnected(string reason) =>
        Dispatcher.UIThread.Post(() => ShowDisconnected(reason));

    // ---------- показ сообщений (поток интерфейса) ----------

    private void ShowMessage(ChatMessage m)
    {
        // Блок истории: подписываем его, чтобы он не путался с новыми сообщениями.
        if (m.Type == MessageTypes.History && !_inHistory)
        {
            _inHistory = true;
            AddNotice("— последние сообщения —", "#8A93AB");
        }
        else if (m.Type != MessageTypes.History && _inHistory)
        {
            _inHistory = false;
            AddNotice("— новые сообщения —", "#8A93AB");
        }

        switch (m.Type)
        {
            case MessageTypes.History:
                AddRow(m.Time, m.Nick, m.Text, "#6B7490", "#8A93AB", null);
                break;

            case MessageTypes.Text:
                bool mine = string.Equals(m.Nick, _myNick, StringComparison.OrdinalIgnoreCase);
                AddRow(m.Time, m.Nick, m.Text, "#1F2A44", mine ? "#4F6EF7" : ColorFor(m.Nick),
                       mine ? "#EEF2FF" : null);
                break;

            case MessageTypes.Private:
                bool fromMe = string.Equals(m.Nick, _myNick, StringComparison.OrdinalIgnoreCase);
                string label = fromMe ? $"Вы → {m.To} (лично)" : $"{m.Nick} → вам (лично)";
                AddRow(m.Time, label, m.Text, "#1F2A44", "#B26A00", "#FFF4D6");
                break;

            case MessageTypes.Notice:
                AddNotice(m.Text, "#6B7490");
                break;

            case MessageTypes.Error:
                AddNotice(m.Text, "#C0392B");
                break;

            case MessageTypes.Users:
                UpdateUsers(m.Users ?? new List<string>());
                break;
        }
    }

    /// <summary>Одна строка ленты: время, ник, текст.</summary>
    private void AddRow(DateTime time, string nick, string text, string textColor, string nickColor, string? background)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };

        var timeBlock = new TextBlock
        {
            Text = time.ToLocalTime().ToString("HH:mm:ss"),
            Foreground = Brush.Parse("#8A93AB"),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        var nickBlock = new TextBlock
        {
            Text = nick,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse(nickColor),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = Brush.Parse(textColor),
            TextWrapping = TextWrapping.Wrap
        };

        Grid.SetColumn(timeBlock, 0);
        Grid.SetColumn(nickBlock, 1);
        Grid.SetColumn(textBlock, 2);
        row.Children.Add(timeBlock);
        row.Children.Add(nickBlock);
        row.Children.Add(textBlock);

        var holder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4),
            Child = row
        };
        if (background != null) holder.Background = Brush.Parse(background);

        AppendToFeed(holder);
    }

    /// <summary>Служебная строка по центру: «вошёл», «вышел», ошибка.</summary>
    private void AddNotice(string text, string color)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = Brush.Parse(color),
            FontStyle = FontStyle.Italic,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2)
        };
        AppendToFeed(block);
    }

    private void AppendToFeed(Control control)
    {
        FeedPanel.Children.Add(control);
        while (FeedPanel.Children.Count > MaxRows) FeedPanel.Children.RemoveAt(0);

        // Прокрутка вниз — после того как лента пересчитает размеры.
        Dispatcher.UIThread.Post(() => FeedScroll.ScrollToEnd(), DispatcherPriority.Background);
    }

    /// <summary>Цвет ника зависит от самого ника, поэтому у каждого собеседника свой.</summary>
    private static string ColorFor(string nick)
    {
        int sum = 0;
        foreach (char c in nick.ToLowerInvariant()) sum += c;
        return NickColors[sum % NickColors.Length];
    }

    // ---------- список «онлайн» ----------

    private void UpdateUsers(List<string> names)
    {
        string? previous = _recipient;
        _updatingUsers = true;
        try
        {
            UsersList.Items.Clear();
            ListBoxItem? toSelect = null;
            foreach (string name in names)
            {
                bool me = string.Equals(name, _myNick, StringComparison.OrdinalIgnoreCase);
                var item = new ListBoxItem
                {
                    Content = me ? $"{name} (вы)" : name,
                    Tag = name,
                    IsEnabled = !me // себе личное сообщение не отправить
                };
                UsersList.Items.Add(item);
                if (!me && previous != null && string.Equals(name, previous, StringComparison.OrdinalIgnoreCase))
                    toSelect = item;
            }
            UsersList.SelectedItem = toSelect;
            _recipient = toSelect?.Tag as string;
        }
        finally
        {
            _updatingUsers = false;
        }

        OnlineHeader.Text = $"Онлайн: {names.Count}";
        UpdateRecipientBar(previous != null && _recipient == null);
    }

    private void UsersList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingUsers) return;
        _recipient = (UsersList.SelectedItem as ListBoxItem)?.Tag as string;
        UpdateRecipientBar(false);
        InputBox.Focus();
    }

    private void ClearRecipientButton_Click(object? sender, RoutedEventArgs e)
    {
        UsersList.SelectedItem = null; // сработает SelectionChanged → получатель сбросится
        _recipient = null;
        UpdateRecipientBar(false);
        InputBox.Focus();
    }

    private void UpdateRecipientBar(bool recipientLeft)
    {
        if (recipientLeft) AddNotice("Получатель личного сообщения вышел — пишете всем.", "#6B7490");

        RecipientBar.IsVisible = _recipient != null;
        RecipientText.Text = _recipient != null ? $"Личное сообщение для: {_recipient}" : "";
    }

    // ---------- отправка ----------

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        SendCurrent();
    }

    private void SendButton_Click(object? sender, RoutedEventArgs e) => SendCurrent();

    private void SendCurrent()
    {
        if (!_connected) return;

        string text = (InputBox.Text ?? "").Trim();
        if (text.Length == 0) return;

        // В очередь на отправку; сама отправка идёт в фоновом потоке, окно не ждёт.
        if (_recipient != null) _client.SendPrivate(_recipient, text);
        else _client.SendText(text);

        InputBox.Text = "";
        InputBox.Focus();
    }

    // ---------- обрыв связи и выход ----------

    private void ShowDisconnected(string reason)
    {
        if (!_connected) return;
        _connected = false;

        AddNotice(reason + " Нажмите «Выйти», чтобы подключиться заново.", "#C0392B");
        StatusText.Text = "Нет соединения";
        InputBox.IsEnabled = false;
        SendButton.IsEnabled = false;
        UsersList.Items.Clear();
        OnlineHeader.Text = "Онлайн";
        _recipient = null;
        RecipientBar.IsVisible = false;
    }

    private void LeaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_leaving) return;
        _leaving = true;

        // Возвращаемся к окну входа; Closed сам отпишется от событий и закроет соединение.
        new LoginWindow().Show();
        Close();
    }
}
