using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chat.Common;

namespace Chat.Client;

/// <summary>Окно входа: адрес, порт, ник. Подключение идёт асинхронно (await), окно не зависает.</summary>
public partial class LoginWindow : Window
{
    private bool _busy;

    public LoginWindow()
    {
        InitializeComponent();

        // Enter в любом поле = нажать «Войти». Tunnel — перехватываем клавишу раньше самого поля.
        AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => NickBox.Focus();
    }

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await ConnectAsync();
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e) => await ConnectAsync();

    private async Task ConnectAsync()
    {
        if (_busy) return;

        string host = (HostBox.Text ?? "").Trim();
        string portText = (PortBox.Text ?? "").Trim();
        string nick = (NickBox.Text ?? "").Trim();

        if (host.Length == 0) { ShowError("Введите адрес сервера."); return; }
        if (!int.TryParse(portText, out int port) || port < 1 || port > 65535)
        {
            ShowError("Порт — число от 1 до 65535.");
            return;
        }
        if (nick.Length == 0) { ShowError("Введите ник."); return; }
        if (nick.Length > ChatDefaults.MaxNickLength)
        {
            ShowError($"Ник не длиннее {ChatDefaults.MaxNickLength} символов.");
            return;
        }

        SetBusy(true);
        var client = new ChatClient();
        try
        {
            await client.ConnectAsync(host, port, nick);

            // Вошли: открываем чат и закрываем окно входа.
            var chat = new ChatWindow(client, host, port);
            chat.Show();
            Close();
            return;
        }
        catch (LoginRejectedException ex)
        {
            ShowError(ex.Message); // например, «Ник уже занят»
        }
        catch (TimeoutException)
        {
            ShowError($"Сервер {host}:{port} не отвечает. Проверьте адрес и порт.");
        }
        catch (SocketException)
        {
            ShowError($"Не удалось подключиться к {host}:{port}. Запущен ли сервер и верны ли адрес и порт?");
        }
        catch (IOException)
        {
            ShowError("Связь с сервером прервалась. Попробуйте ещё раз.");
        }
        catch (Exception ex)
        {
            ShowError("Ошибка подключения: " + ex.Message);
        }

        SetBusy(false);
    }

    private void ShowError(string text)
    {
        ErrorText.Text = text;
        ErrorText.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (busy) ErrorText.IsVisible = false;
        HostBox.IsEnabled = !busy;
        PortBox.IsEnabled = !busy;
        NickBox.IsEnabled = !busy;
        ConnectButton.IsEnabled = !busy;
        ConnectButton.Content = busy ? "Подключение…" : "Войти";
    }
}
