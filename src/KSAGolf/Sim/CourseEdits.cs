namespace KSAGolf;

/// <summary>A course being built, with its undo and redo. Courses are immutable, so a step back is the course before.</summary>
internal sealed class CourseEdits(Course start)
{
    private readonly Stack<Course> _undo = new();
    private readonly Stack<Course> _redo = new();

    public Course Current { get; private set; } = start;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Apply(Course next)
    {
        if (ReferenceEquals(next, Current)) return;

        _undo.Push(Current);
        _redo.Clear();
        Current = next;
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;

        _redo.Push(Current);
        Current = _undo.Pop();
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;

        _undo.Push(Current);
        Current = _redo.Pop();
        return true;
    }
}
