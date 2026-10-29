using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TodoApp;

public partial class MainWindow : Window
{
    private const string DateFormat = "dd.MM.yyyy";

    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "todos.json");
    private TodoList _list = new();
    private bool _refreshing;      
    private string? _startupError;  
    public MainWindow()
    {
        InitializeComponent();

        LoadTasks();
        RefreshList();

        SearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) RefreshList();
        };
        FilterBox.SelectionChanged += (_, _) => RefreshList();
        SortCheck.IsCheckedChanged += (_, _) => RefreshList();
        TaskList.SelectionChanged += TaskList_SelectionChanged;

        Opened += async (_, _) =>
        {
            if (_startupError != null)
                await ShowError(_startupError);
        };
    }


    private async void AddButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var due = ParseDueDate(DueBox.Text);     
            _list.Add(TitleBox.Text ?? "", due);     
            TitleBox.Text = "";
            DueBox.Text = "";
            RefreshList();
            await SaveTasks();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            await ShowError(ex.Message);
        }
    }

    private async void DoneButton_Click(object? sender, RoutedEventArgs e)
    {
        var item = await GetSelectedOrWarn();
        if (item == null) return;

        try
        {
            _list.Complete(item);
            RefreshList();
            await SaveTasks();
        }
        catch (ArgumentException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void RenameButton_Click(object? sender, RoutedEventArgs e)
    {
        var item = await GetSelectedOrWarn();
        if (item == null) return;

        try
        {
            _list.Rename(item, TitleBox.Text ?? "");
            RefreshList();
            await SaveTasks();
        }
        catch (ArgumentException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        var item = await GetSelectedOrWarn();
        if (item == null) return;

        try
        {
            _list.Remove(item);
            TitleBox.Text = "";
            RefreshList();
            await SaveTasks();
        }
        catch (ArgumentException ex)
        {
            await ShowError(ex.Message);
        }
    }

    private void TaskList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;

        var item = SelectedTask;
        if (item != null)
            TitleBox.Text = item.Title;
    }


    private TodoItem? SelectedTask =>
        (TaskList.SelectedItem as ListBoxItem)?.Tag as TodoItem;

    private async Task<TodoItem?> GetSelectedOrWarn()
    {
        var item = SelectedTask;
        if (item == null)
            await ShowError("Сначала выберите задачу в списке");
        return item;
    }

    private static DateTime? ParseDueDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null; // срок необязателен

        if (!DateTime.TryParseExact(text.Trim(), DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new FormatException("Срок нужно ввести в формате дд.мм.гггг, например 31.10.2026");

        return date;
    }

    private void RefreshList()
    {
        var selected = SelectedTask;
        var today = DateTime.Today;
        var filter = (TodoFilter)Math.Max(FilterBox.SelectedIndex, 0);
        var view = _list.GetView(filter, SearchBox.Text ?? "", SortCheck.IsChecked == true);

        _refreshing = true;
        TaskList.Items.Clear();

        foreach (var item in view)
        {
            var row = CreateRow(item, today);
            TaskList.Items.Add(row);
            if (item == selected)
                TaskList.SelectedItem = row;
        }

        _refreshing = false;
    }

    private static ListBoxItem CreateRow(TodoItem item, DateTime today)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(150, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(110, GridUnitType.Pixel));

        var title = new TextBlock
        {
            Text = item.Title,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var due = new TextBlock
        {
            Text = item.DueDate.HasValue ? "срок: " + item.DueDate.Value.ToString(DateFormat) : "без срока"
        };
        var status = new TextBlock { Text = item.IsDone ? "выполнено" : (item.IsOverdue(today) ? "не выполнено" : "активна") };

        Grid.SetColumn(due, 1);
        Grid.SetColumn(status, 2);
        grid.Children.Add(title);
        grid.Children.Add(due);
        grid.Children.Add(status);

        var row = new ListBoxItem { Content = grid, Tag = item };

        if (item.IsDone)
            row.Classes.Add("done");
        else if (item.IsOverdue(today)) 
            row.Classes.Add("overdue");

        return row;
    }

    private void LoadTasks()
    {
        try
        {
            _list = TodoList.Load(_filePath); 
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
                                       or UnauthorizedAccessException)
        {
            _startupError = "Не удалось загрузить задачи: " + ex.Message;
            _list = new TodoList();
        }
    }

    private async Task SaveTasks()
    {
        try
        {
            _list.Save(_filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowError("Не удалось сохранить задачи: " + ex.Message);
        }
    }

    private async Task ShowError(string message)
    {
        var okButton = new Button
        {
            Content = "OK",
            MinWidth = 80,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var dialog = new Window
        {
            Title = "Ошибка",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    okButton
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
