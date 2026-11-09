using System.Collections.Generic;

namespace PaintApp;

/// <summary>История команд: два стека. Что можно отменить и что можно повторить.</summary>
public class History
{
    private readonly Stack<ICommand> _undo = new();   // что можно отменить
    private readonly Stack<ICommand> _redo = new();   // что можно повторить

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Выполнить новое действие и запомнить его.</summary>
    public void Do(ICommand command)
    {
        command.Execute();
        _undo.Push(command);
        _redo.Clear();          // новое действие: повторять больше нечего
    }

    /// <summary>Отменить: команда из _undo, вызвать у неё Undo(), положить в _redo.</summary>
    public void Undo()
    {
        if (!CanUndo)
            return;

        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
    }

    /// <summary>Повторить: наоборот.</summary>
    public void Redo()
    {
        if (!CanRedo)
            return;

        var command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
    }

    /// <summary>Забыть всё (например, после открытия другого файла).</summary>
    public void Reset()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
