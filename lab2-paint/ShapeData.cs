using System.Collections.Generic;

namespace PaintApp;

/// <summary>Точка в файле.</summary>
public record PointData(double X, double Y);

/// <summary>Как одна фигура выглядит в JSON-файле: тип, цвет, толщина и все точки.
/// У линии, прямоугольника и эллипса две точки, у карандаша столько, сколько нарисовано.</summary>
public record ShapeData(string Kind, string Color, int Width, List<PointData> Points);
