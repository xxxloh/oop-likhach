using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace PaintApp;

/// <summary>Окно: инструменты, цвет, толщина, мышь, кнопки. Фигуры хранит рисунок (_drawing),
/// а не окно. Все действия над рисунком идут через историю команд (_history).</summary>
public partial class MainWindow : Window
{
    private const string SelectTool = "select"; // не фигура, а режим выделения (★)

    private static readonly string[] PaletteHex =
    {
        "#000000", "#E53935", "#FB8C00", "#FDD835", "#43A047", "#00ACC1",
        "#1E88E5", "#8E24AA", "#EC407A", "#6D4C41", "#9E9E9E"
    };

    private static readonly FilePickerFileType JsonFiles =
        new("Рисунок (JSON)") { Patterns = new[] { "*.json" } };

    private static readonly FilePickerFileType PngFiles =
        new("Картинка (PNG)") { Patterns = new[] { "*.png" } };

    private Drawing _drawing = new();
    private readonly History _history = new();

    private string _tool = "LineShape";
    private Color _color = Colors.Black;

    private Shape? _preview;    // фигура, которую сейчас тянут
    private Shape? _selected;   // выделенная фигура
    private Shape? _dragged;    // фигура, которую сейчас двигают
    private Point _dragLast;
    private Vector _dragTotal;

    public MainWindow()
    {
        InitializeComponent();

        PaintCanvas.Drawing = _drawing;
        BuildToolButtons();
        BuildColorSwatches();

        WidthSlider.ValueChanged += (_, _) => UpdateWidthLabel();
        UpdateWidthLabel();

        // Мышь
        CanvasHost.PointerPressed += Canvas_PointerPressed;
        CanvasHost.PointerMoved += Canvas_PointerMoved;
        CanvasHost.PointerReleased += Canvas_PointerReleased;
        CanvasHost.PointerCaptureLost += Canvas_PointerCaptureLost;

        // Клавиши: Ctrl+Z, Ctrl+Y, Delete
        KeyDown += Window_KeyDown;

        ChooseTool(_tool);
    }

    // ---------- построение панели инструментов ----------

    /// <summary>Кнопки инструментов делаются по фабрике: новая фигура появится сама.</summary>
    private void BuildToolButtons()
    {
        AddToolButton(SelectTool, "Выделение");

        foreach (var kind in ShapeFactory.Kinds.Keys)
        {
            var title = ShapeFactory.Create(kind, new Point(), Colors.Black, 1).Title;
            AddToolButton(kind, title);
        }
    }

    private void AddToolButton(string tool, string title)
    {
        var button = new ToggleButton { Content = title, Tag = tool };
        button.Click += (_, _) => ChooseTool(tool);
        ToolsPanel.Children.Add(button);
    }

    private void BuildColorSwatches()
    {
        foreach (var hex in PaletteHex)
        {
            var color = Color.Parse(hex);
            var swatch = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(3),
                BorderBrush = Brushes.Transparent,
                Margin = new Thickness(3, 0),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = color
            };
            swatch.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(swatch).Properties.IsLeftButtonPressed)
                    ChooseColor(color);
            };
            ColorsPanel.Children.Add(swatch);
        }

        ChooseColor(Color.Parse(PaletteHex[0]));
    }

    private void ChooseColor(Color color)
    {
        _color = color;
        foreach (var swatch in ColorsPanel.Children.OfType<Border>())
            swatch.BorderBrush = swatch.Tag is Color c && c == color
                ? Brushes.DodgerBlue
                : Brushes.Transparent;
    }

    private void ChooseTool(string tool)
    {
        _tool = tool;

        // Выбранный инструмент виден: его кнопка нажата
        foreach (var button in ToolsPanel.Children.OfType<ToggleButton>())
            button.IsChecked = (button.Tag as string) == tool;

        if (tool != SelectTool)
            _selected = null;

        CanvasHost.Cursor = new Cursor(tool == SelectTool ? StandardCursorType.Arrow : StandardCursorType.Cross);
        StatusText.Text = tool == SelectTool
            ? "Выделение: щёлкните по фигуре, перетащите её или нажмите Delete. Отмена: Ctrl/⌘+Z, повтор: Ctrl/⌘+Y."
            : "Рисование: протяните мышью по холсту. Отмена: Ctrl/⌘+Z, повтор: Ctrl/⌘+Y.";

        AfterChange();
    }

    private int CurrentWidth => (int)Math.Round(WidthSlider.Value);

    private void UpdateWidthLabel() => WidthLabel.Text = $"Толщина: {CurrentWidth}";

    // ---------- мышь ----------

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(PaintCanvas);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var position = point.Position;

        if (_tool == SelectTool)
        {
            // ★ щелчок: найти фигуру под курсором и начать её тянуть
            _selected = _drawing.FindAt(position);
            if (_selected != null)
            {
                _dragged = _selected;
                _dragLast = position;
                _dragTotal = default;
                e.Pointer.Capture(CanvasHost);
            }
        }
        else
        {
            // Нажатие: создать фигуру. Какого она класса, решает фабрика по имени инструмента
            _selected = null;
            _preview = ShapeFactory.Create(_tool, position, _color, CurrentWidth);
            e.Pointer.Capture(CanvasHost);
        }

        AfterChange();
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(PaintCanvas);

        if (_preview != null)
        {
            // Движение с нажатой кнопкой: сдвинуть вторую точку и перерисовать (предпросмотр)
            _preview.MoveEnd(position);
            Refresh();
        }
        else if (_dragged != null)
        {
            var delta = position - _dragLast;
            _dragged.Move(delta.X, delta.Y);
            _dragLast = position;
            _dragTotal += delta;
            Refresh();
        }
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        if (_preview != null)
        {
            // Отпускание: фигура готова. Добавляем через историю, чтобы можно было отменить
            var shape = _preview;
            _preview = null;
            shape.MoveEnd(e.GetPosition(PaintCanvas));

            if (!shape.IsDegenerate)
                _history.Do(new AddShapeCommand(_drawing, shape));
        }
        else if (_dragged != null)
        {
            // Фигура уже стоит на новом месте. Возвращаем её назад и делаем перемещение
            // командой, чтобы Ctrl+Z вернул её на старое место
            var shape = _dragged;
            var total = _dragTotal;
            _dragged = null;
            _dragTotal = default;

            if (total != default)
            {
                shape.Move(-total.X, -total.Y);
                _history.Do(new MoveShapeCommand(shape, total.X, total.Y));
            }
        }

        e.Pointer.Capture(null);
        AfterChange();
    }

    // Жест прервали (например, окно потеряло фокус): ничего не добавляем
    private void Canvas_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragged != null)
        {
            _dragged.Move(-_dragTotal.X, -_dragTotal.Y);
            _dragged = null;
            _dragTotal = default;
        }

        _preview = null;
        AfterChange();
    }

    // ---------- клавиши ----------

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        // На Mac привычнее Cmd, на других системах Ctrl: принимаем оба
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                      || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (command && e.Key == Key.Z)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                Redo();
            else
                Undo();
            e.Handled = true;
        }
        else if (command && e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Back)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    // ---------- действия ----------

    private void Undo()
    {
        _history.Undo();
        AfterChange();
    }

    private void Redo()
    {
        _history.Redo();
        AfterChange();
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        _history.Do(new RemoveShapeCommand(_drawing, _selected));
        AfterChange();
    }

    private void UndoButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Undo();

    private void RedoButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Redo();

    private void DeleteButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        DeleteSelected();

    private void ClearButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_drawing.Count == 0)
            return;

        _history.Do(new ClearCommand(_drawing));
        AfterChange();
    }

    // ---------- файлы ----------

    private async void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить рисунок",
                SuggestedFileName = "рисунок.json",
                DefaultExtension = "json",
                FileTypeChoices = new[] { JsonFiles }
            });
            if (file == null)
                return; // пользователь отказался

            var path = file.TryGetLocalPath();
            if (path == null)
            {
                await ShowError("Не удалось определить путь к файлу");
                return;
            }

            _drawing.Save(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowError("Не удалось сохранить рисунок: " + ex.Message);
        }
    }

    private async void OpenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Открыть рисунок",
                AllowMultiple = false,
                FileTypeFilter = new[] { JsonFiles }
            });
            if (files.Count == 0)
                return;

            var path = files[0].TryGetLocalPath();
            if (path == null)
            {
                await ShowError("Не удалось определить путь к файлу");
                return;
            }

            // Фигуры нужных классов восстанавливает фабрика (внутри Drawing.Load)
            _drawing = Drawing.Load(path);
            PaintCanvas.Drawing = _drawing;
            _history.Reset();   // команды относились к старому рисунку
            _selected = null;
            AfterChange();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
                                       or UnauthorizedAccessException)
        {
            await ShowError("Не удалось открыть рисунок: " + ex.Message);
        }
    }

    /// <summary>★★ Экспорт рисунка в PNG.</summary>
    private async void ExportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var width = (int)PaintCanvas.Bounds.Width;
        var height = (int)PaintCanvas.Bounds.Height;
        if (width < 1 || height < 1)
        {
            await ShowError("Холст пустой, экспортировать нечего");
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт в PNG",
                SuggestedFileName = "рисунок.png",
                DefaultExtension = "png",
                FileTypeChoices = new[] { PngFiles }
            });
            if (file == null)
                return;

            var path = file.TryGetLocalPath();
            if (path == null)
            {
                await ShowError("Не удалось определить путь к файлу");
                return;
            }

            // Рамку выделения и предпросмотр в картинку не берём
            PaintCanvas.ShowOverlays = false;
            try
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
                bitmap.Render(PaintCanvas);
                bitmap.Save(path);
            }
            finally
            {
                PaintCanvas.ShowOverlays = true;
                PaintCanvas.InvalidateVisual();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ShowError("Не удалось сохранить картинку: " + ex.Message);
        }
    }

    // ---------- обновление окна ----------

    /// <summary>Передать холсту текущее состояние и перерисовать.</summary>
    private void Refresh()
    {
        PaintCanvas.Drawing = _drawing;
        PaintCanvas.Preview = _preview;
        PaintCanvas.Selected = _selected;
        PaintCanvas.InvalidateVisual();
    }

    /// <summary>Вызывается после любого изменения: убирает выделение с пропавшей фигуры,
    /// перерисовывает холст и включает/выключает кнопки.</summary>
    private void AfterChange()
    {
        if (_selected != null && _drawing.IndexOf(_selected) < 0)
            _selected = null;

        Refresh();

        UndoButton.IsEnabled = _history.CanUndo;
        RedoButton.IsEnabled = _history.CanRedo;
        DeleteButton.IsEnabled = _selected != null;
        ClearButton.IsEnabled = _drawing.Count > 0;
    }

    /// <summary>В Avalonia нет готового MessageBox, поэтому делаем маленькое окно сами.</summary>
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
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
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
