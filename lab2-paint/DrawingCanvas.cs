using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PaintApp;

/// <summary>Холст: каждый раз перерисовывается по списку фигур рисунка.
/// Сам ничего не хранит и мышь не обрабатывает, это делает окно.</summary>
public class DrawingCanvas : Control
{
    private static readonly Pen SelectionPen = new(Brushes.DodgerBlue, 1.5, DashStyle.Dash);

    /// <summary>Рисунок, который показываем.</summary>
    public Drawing Drawing { get; set; } = new();

    /// <summary>Фигура, которую сейчас тянут мышью (предпросмотр).</summary>
    public Shape? Preview { get; set; }

    /// <summary>Выделенная фигура (пунктирная рамка).</summary>
    public Shape? Selected { get; set; }

    /// <summary>false при экспорте в PNG: рамка выделения и предпросмотр в картинку не попадают.</summary>
    public bool ShowOverlays { get; set; } = true;

    public override void Render(DrawingContext context)
    {
        var area = new Rect(Bounds.Size);
        context.DrawRectangle(Brushes.White, null, area);

        using (context.PushClip(area))
        {
            Drawing.Draw(context);

            if (!ShowOverlays)
                return;

            Preview?.Draw(context);

            if (Selected != null)
                context.DrawRectangle(null, SelectionPen, Selected.GetBounds().Inflate(4));
        }
    }
}
