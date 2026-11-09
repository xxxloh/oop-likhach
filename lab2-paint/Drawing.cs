using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;

namespace PaintApp;

/// <summary>Рисунок содержит список фигур (композиция): добавить, убрать, очистить,
/// нарисовать, сохранить, загрузить. Окно в модели не участвует.</summary>
public class Drawing
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Тип Shape, а не конкретные классы: список хранит любые фигуры
    private readonly List<Shape> _shapes = new();

    public IReadOnlyList<Shape> Shapes => _shapes;
    public int Count => _shapes.Count;

    public void Add(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        _shapes.Add(shape);
    }

    public void Insert(int index, Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        _shapes.Insert(index, shape);
    }

    public bool Remove(Shape shape) => _shapes.Remove(shape);

    public int IndexOf(Shape shape) => _shapes.IndexOf(shape);

    /// <summary>Убрать последнюю фигуру. Возвращает её или null, если рисунок пуст.</summary>
    public Shape? RemoveLast()
    {
        if (_shapes.Count == 0)
            return null;

        var last = _shapes[^1];
        _shapes.RemoveAt(_shapes.Count - 1);
        return last;
    }

    public void Clear() => _shapes.Clear();

    /// <summary>Фигура под точкой. Идём с конца: верхняя фигура важнее.</summary>
    public Shape? FindAt(Point p)
    {
        for (var i = _shapes.Count - 1; i >= 0; i--)
            if (_shapes[i].Contains(p))
                return _shapes[i];
        return null;
    }

    /// <summary>Один цикл на все фигуры. Какой именно у каждой Draw, мы не знаем и знать не нужно:
    /// вызовется Draw того класса, которым объект является на самом деле (полиморфизм).</summary>
    public void Draw(DrawingContext context)
    {
        foreach (var shape in _shapes)
            shape.Draw(context);
    }

    public void Save(string path)
    {
        var data = _shapes
            .Select(s => new ShapeData(
                s.Kind,
                s.Color.ToString(),
                s.Width,
                s.GetPoints().Select(p => new PointData(p.X, p.Y)).ToList()))
            .ToList();

        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
    }

    /// <summary>Читает файл и создаёт фигуры нужных классов через фабрику.
    /// Испорченный файл: InvalidDataException.</summary>
    public static Drawing Load(string path)
    {
        var drawing = new Drawing();
        try
        {
            var data = JsonSerializer.Deserialize<List<ShapeData>>(File.ReadAllText(path))
                       ?? throw new InvalidDataException("Файл пустой");

            foreach (var item in data)
                drawing._shapes.Add(Restore(item));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            throw new InvalidDataException("Файл рисунка повреждён или создан другой программой", ex);
        }

        return drawing;
    }

    private static Shape Restore(ShapeData data)
    {
        if (data == null || data.Points == null || data.Points.Count == 0)
            throw new ArgumentException("У фигуры нет точек");
        if (data.Width is < 1 or > 100)
            throw new ArgumentException("Неверная толщина");

        var points = data.Points.Select(p => new Point(p.X, p.Y)).ToList();

        // Тип из файла превращает в объект фабрика (одно место на всю программу)
        var shape = ShapeFactory.Create(data.Kind, points[0], Color.Parse(data.Color), data.Width);
        for (var i = 1; i < points.Count; i++)
            shape.MoveEnd(points[i]);
        return shape;
    }
}
