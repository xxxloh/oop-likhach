using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PaintApp;

/// <summary>Карандаш: хранит список точек и рисует ломаную.</summary>
public class PencilShape : Shape
{
    private readonly List<Point> _points = new();

    public PencilShape(Point start, Color color, int width) : base(start, color, width)
    {
        _points.Add(start);
    }

    public override string Title => "Карандаш";

    // Карандаш не двигает вторую точку, а добавляет новую
    public override void MoveEnd(Point p)
    {
        base.MoveEnd(p);
        _points.Add(p);
    }

    public override IReadOnlyList<Point> GetPoints() => _points;

    public override void Move(double dx, double dy)
    {
        base.Move(dx, dy);
        var shift = new Vector(dx, dy);
        for (var i = 0; i < _points.Count; i++)
            _points[i] += shift;
    }

    // Даже один клик карандашом оставляет точку
    public override bool IsDegenerate => false;

    public override Rect GetBounds()
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in _points)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY).Inflate(Width / 2.0);
    }

    public override void Draw(DrawingContext context)
    {
        if (_points.Count == 1)
        {
            // одна точка: рисуем кружок
            context.DrawEllipse(new SolidColorBrush(Color), null, _points[0], Width / 2.0, Width / 2.0);
            return;
        }

        var pen = CreatePen();
        for (var i = 1; i < _points.Count; i++)
            context.DrawLine(pen, _points[i - 1], _points[i]);
    }

    // Карандаш: точка рядом с любым звеном ломаной
    public override bool Contains(Point p)
    {
        var limit = Width / 2.0 + 5;
        if (_points.Count == 1)
            return Distance(p, _points[0]) <= limit;

        for (var i = 1; i < _points.Count; i++)
            if (DistanceToSegment(p, _points[i - 1], _points[i]) <= limit)
                return true;
        return false;
    }
}
