using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PaintApp;

/// <summary>Общее для всех фигур: две точки, цвет, толщина.
/// «Просто фигуру» нарисовать нельзя, поэтому класс абстрактный.</summary>
public abstract class Shape
{
    public Point Start { get; protected set; }
    public Point End { get; protected set; }
    public Color Color { get; }
    public int Width { get; }

    protected Shape(Point start, Color color, int width)
    {
        Start = End = start;
        Color = color;
        Width = width;
    }

    /// <summary>Название для кнопки инструмента.</summary>
    public abstract string Title { get; }

    /// <summary>Имя типа: по нему фигура записывается в файл и создаётся фабрикой.</summary>
    public string Kind => GetType().Name;

    /// <summary>Пока мышь тянут, сдвигается вторая точка. Карандаш переопределяет метод.</summary>
    public virtual void MoveEnd(Point p) => End = p;

    /// <summary>Точки фигуры для сохранения в файл.</summary>
    public virtual IReadOnlyList<Point> GetPoints() => new[] { Start, End };

    /// <summary>Сдвинуть фигуру целиком (★ перемещение).</summary>
    public virtual void Move(double dx, double dy)
    {
        var shift = new Vector(dx, dy);
        Start += shift;
        End += shift;
    }

    /// <summary>Нулевая фигура (клик без протягивания) не добавляется в рисунок.</summary>
    public virtual bool IsDegenerate => Start == End;

    // Прямоугольник по двум точкам: тянуть можно в любую сторону
    protected Rect Bounds() => new(
        Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
        Math.Abs(End.X - Start.X), Math.Abs(End.Y - Start.Y));

    /// <summary>Рамка фигуры с учётом толщины линии (для выделения).</summary>
    public virtual Rect GetBounds() => Bounds().Inflate(Width / 2.0);

    protected Pen CreatePen() =>
        new(new SolidColorBrush(Color), Width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    public abstract void Draw(DrawingContext context);

    /// <summary>Попадает ли точка в фигуру (★ выделение щелчком).
    /// Каждая фигура решает это по-своему.</summary>
    public abstract bool Contains(Point p);

    protected static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Расстояние от точки до отрезка.</summary>
    protected static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0)
            return Distance(p, a);

        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        return Distance(p, new Point(a.X + t * dx, a.Y + t * dy));
    }
}
