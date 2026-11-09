using Avalonia;
using Avalonia.Media;

namespace PaintApp;

public class RectShape : Shape
{
    public RectShape(Point start, Color color, int width) : base(start, color, width) { }

    public override string Title => "Прямоугольник";

    public override void Draw(DrawingContext context) =>
        context.DrawRectangle(null, CreatePen(), Bounds());

    // Прямоугольник: точка внутри рамки фигуры
    public override bool Contains(Point p) => GetBounds().Contains(p);
}
