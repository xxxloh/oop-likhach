using System;
using System.Text.Json.Serialization;

namespace TodoApp;

public class TodoItem
{
    private string _title = "";
    private bool _done;
    private DateTime? _dueDate;

    [JsonConstructor]
    public TodoItem(string title, DateTime? dueDate = null, bool isDone = false)
    {
        Rename(title);
        _dueDate = dueDate?.Date;
        _done = isDone;
    }

    public string Title => _title;
    public bool IsDone => _done;
    public DateTime? DueDate => _dueDate;

    public void Complete() => _done = true;

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Название не может быть пустым");
        _title = title.Trim();
    }

    public void SetDueDate(DateTime? dueDate) => _dueDate = dueDate?.Date;

    public bool IsOverdue(DateTime today) =>
        !_done && _dueDate.HasValue && _dueDate.Value < today.Date;
}
