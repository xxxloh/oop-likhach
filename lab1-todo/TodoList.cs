using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TodoApp;

public enum TodoFilter
{
    All,
    Active,
    Done,
    Overdue
}

public class TodoList
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly List<TodoItem> _items = new();

    public IReadOnlyList<TodoItem> Items => _items;
    public int Count => _items.Count;

    public TodoItem Add(string title, DateTime? dueDate = null)
    {
        var item = new TodoItem(title, dueDate); // пустое название -> ArgumentException
        _items.Add(item);
        return item;
    }

    public void Remove(TodoItem item)
    {
        if (!_items.Remove(item))
            throw new ArgumentException("Такой задачи нет в списке");
    }

    public void Complete(TodoItem item)
    {
        EnsureContains(item);
        item.Complete();
    }

    public void Rename(TodoItem item, string newTitle)
    {
        EnsureContains(item);
        item.Rename(newTitle);
    }

    public IReadOnlyList<TodoItem> GetView(
        TodoFilter filter = TodoFilter.All,
        string search = "",
        bool sortByDueDate = false)
    {
        IEnumerable<TodoItem> result = _items;

    var today = DateTime.Today;

        result = filter switch
        {
            TodoFilter.Active => result.Where(i => !i.IsDone),
            TodoFilter.Done => result.Where(i => i.IsDone),
            TodoFilter.Overdue => result.Where(i => !i.IsDone && i.IsOverdue(today)),
    _ => result
        };

        var text = (search ?? "").Trim();
        if (text.Length > 0)
            result = result.Where(i =>
                i.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase));

        if (sortByDueDate)
            result = result.OrderBy(i => i.DueDate ?? DateTime.MaxValue);

        return result.ToList();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(_items, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static TodoList Load(string path)
    {
        var list = new TodoList();
        if (!File.Exists(path))
            return list;

        try
        {
            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<TodoItem>>(json);
            if (items != null)
                list._items.AddRange(items);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Файл с задачами повреждён", ex);
        }

        return list;
    }

    private void EnsureContains(TodoItem item)
    {
        if (item == null || !_items.Contains(item))
            throw new ArgumentException("Такой задачи нет в списке");
    }
}
