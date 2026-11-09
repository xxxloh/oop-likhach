using System.Collections.Generic;
using System.Linq;

namespace PaintApp;

/// <summary>Паттерн Команда: действие это объект, который умеет себя выполнить и отменить.</summary>
public interface ICommand
{
    void Execute();
    void Undo();
}

public class AddShapeCommand : ICommand
{
    private readonly Drawing _drawing;
    private readonly Shape _shape;

    public AddShapeCommand(Drawing drawing, Shape shape)
    {
        _drawing = drawing;
        _shape = shape;
    }

    public void Execute() => _drawing.Add(_shape);
    public void Undo() => _drawing.Remove(_shape);
}

/// <summary>Удаление фигуры. Запоминает место в списке, чтобы отмена вернула её на то же место.</summary>
public class RemoveShapeCommand : ICommand
{
    private readonly Drawing _drawing;
    private readonly Shape _shape;
    private int _index;

    public RemoveShapeCommand(Drawing drawing, Shape shape)
    {
        _drawing = drawing;
        _shape = shape;
    }

    public void Execute()
    {
        _index = _drawing.IndexOf(_shape);
        _drawing.Remove(_shape);
    }

    public void Undo() => _drawing.Insert(_index < 0 ? _drawing.Count : _index, _shape);
}

/// <summary>Очистка холста. Запоминает все фигуры, чтобы вернуть их при отмене.</summary>
public class ClearCommand : ICommand
{
    private readonly Drawing _drawing;
    private List<Shape> _backup = new();

    public ClearCommand(Drawing drawing) => _drawing = drawing;

    public void Execute()
    {
        _backup = _drawing.Shapes.ToList();
        _drawing.Clear();
    }

    public void Undo()
    {
        foreach (var shape in _backup)
            _drawing.Add(shape);
    }
}

/// <summary>Перемещение фигуры на (dx, dy).</summary>
public class MoveShapeCommand : ICommand
{
    private readonly Shape _shape;
    private readonly double _dx;
    private readonly double _dy;

    public MoveShapeCommand(Shape shape, double dx, double dy)
    {
        _shape = shape;
        _dx = dx;
        _dy = dy;
    }

    public void Execute() => _shape.Move(_dx, _dy);
    public void Undo() => _shape.Move(-_dx, -_dy);
}
