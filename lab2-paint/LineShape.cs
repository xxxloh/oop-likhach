using Avalonia;
using Avalonia.Media;

namespace PaintApp;

public class LineShape : Shape
{
    public LineShape(Point start, Color color, int width) : base(start, color, width) { }

    public override string Title => "Линия";

    public override void Draw(DrawingContext context) =>
        context.DrawLine(CreatePen(), Start, End);

    // Линия: точка должна быть рядом с отрезком
    public override bool Contains(Point p) =>
        DistanceToSegment(p, Start, End) <= Width / 2.0 + 5;
}
