using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PaintApp;

/// <summary>Фабрика: «имя типа → как создать». Единственное место, где имя превращается
/// в объект: по нему окно делает кнопки, мышь создаёт фигуры, загрузка читает файл.
/// Новая фигура = новый класс и одна строка в словаре.</summary>
public static class ShapeFactory
{
    public static readonly Dictionary<string, Func<Point, Color, int, Shape>> Kinds = new()
    {
        ["LineShape"] = (p, c, w) => new LineShape(p, c, w),
        ["RectShape"] = (p, c, w) => new RectShape(p, c, w),
        ["EllipseShape"] = (p, c, w) => new EllipseShape(p, c, w),
        ["PencilShape"] = (p, c, w) => new PencilShape(p, c, w),
    };

    public static Shape Create(string kind, Point start, Color color, int width)
    {
        if (!Kinds.TryGetValue(kind, out var create))
            throw new ArgumentException($"Неизвестная фигура: {kind}");
        return create(start, color, width);
    }
}
