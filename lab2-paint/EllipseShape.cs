using Avalonia;
using Avalonia.Media;

namespace PaintApp;

public class EllipseShape : Shape
{
    public EllipseShape(Point start, Color color, int width) : base(start, color, width) { }

    public override string Title => "Эллипс";

    public override void Draw(DrawingContext context)
    {
        var r = Bounds();
        var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        context.DrawEllipse(null, CreatePen(), center, r.Width / 2, r.Height / 2);
    }

    // Эллипс: точка удовлетворяет уравнению эллипса (внутри него)
    public override bool Contains(Point p)
    {
        var r = GetBounds();
        var rx = r.Width / 2;
        var ry = r.Height / 2;
        if (rx < 1 || ry < 1)
            return r.Inflate(3).Contains(p);

        var nx = (p.X - (r.X + rx)) / rx;
        var ny = (p.Y - (r.Y + ry)) / ry;
        return nx * nx + ny * ny <= 1;
    }
}
