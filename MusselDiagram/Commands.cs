namespace MusselDiagram;

using System.Diagnostics.CodeAnalysis;

public static class DiagramCommands
{
    public const string Undo = nameof(Undo);

    public const string Redo = nameof(Redo);

    public const string Delete = nameof(Delete);

    public const string Cut = nameof(Cut);

    public const string Copy = nameof(Copy);

    public const string Paste = nameof(Paste);

    public const string SelectAll = nameof(SelectAll);

    public const string ClearSelection = nameof(ClearSelection);

    public const string EditText = nameof(EditText);

    public const string QuickAdd = nameof(QuickAdd);

    public const string Group = nameof(Group);

    public const string Ungroup = nameof(Ungroup);

    public const string CreateOverlayGroup = nameof(CreateOverlayGroup);

    public const string AddMembers = nameof(AddMembers);

    public const string RemoveMembers = nameof(RemoveMembers);

    public const string BringToFront = nameof(BringToFront);

    public const string SendToBack = nameof(SendToBack);

    public const string BringForward = nameof(BringForward);

    public const string SendBackward = nameof(SendBackward);

    public const string AlignLeft = nameof(AlignLeft);

    public const string AlignCenter = nameof(AlignCenter);

    public const string AlignRight = nameof(AlignRight);

    public const string AlignTop = nameof(AlignTop);

    public const string AlignMiddle = nameof(AlignMiddle);

    public const string AlignBottom = nameof(AlignBottom);

    public const string DistributeHorizontally = nameof(DistributeHorizontally);

    public const string DistributeVertically = nameof(DistributeVertically);

    public const string ToggleLock = nameof(ToggleLock);

    public const string ZoomIn = nameof(ZoomIn);

    public const string ZoomOut = nameof(ZoomOut);

    public const string ResetZoom = nameof(ResetZoom);

    public const string ZoomToFit = nameof(ZoomToFit);

    public const string ZoomToSelection = nameof(ZoomToSelection);

    public const string NextIssue = nameof(NextIssue);

    public const string PreviousIssue = nameof(PreviousIssue);
}

public sealed class DiagramCommandContext
{
    internal DiagramCommandContext(MusselDiagramView view, IReadOnlyList<string> selectedIds, object? argument)
    {
        View = view;
        SelectedIds = selectedIds;
        Argument = argument;
    }

    public MusselDiagramView View { get; }

    public Diagram Diagram => View.Diagram;

    public IReadOnlyList<string> SelectedIds { get; }

    public object? Argument { get; }
}

public sealed class DiagramCommand
{
    private Func<DiagramCommandContext, bool>? appCanExecute;

    private IReadOnlyList<string> keys = [];

    public required string Name { get; init; }

    public string? DisplayName { get => field ?? DefaultName?.Invoke(Name); init; }

    public IReadOnlyList<string> Keys { get => keys; init => keys = value; }

    public bool IsEnabled { get; internal set; }

    public bool IsChecked { get; internal set; }

    public Func<DiagramCommandContext, bool>? CanExecute { get; init; }

    public Func<DiagramCommandContext, bool>? GetIsChecked { get; init; }

    public required Func<DiagramCommandContext, Task> Execute { get; init; }

    internal bool Local { get; init; }

    internal Func<string, string>? DefaultName { get; init; }

    internal Func<DiagramCommandContext, Task>? Replacement { get; set; }

    internal bool RunsLocally => Local && (Replacement is null) && (appCanExecute is null);

    internal void ReplaceKeys(IReadOnlyList<string> value) => keys = value;

    internal void AddCanExecute(Func<DiagramCommandContext, bool> canExecute) => appCanExecute = canExecute;

    internal bool Evaluate(DiagramCommandContext context) =>
        (CanExecute?.Invoke(context) ?? true) && (appCanExecute?.Invoke(context) ?? true);

    internal Task RunAsync(DiagramCommandContext context) => (Replacement ?? Execute)(context);
}

public sealed class DiagramCommandRegistry
{
    private static readonly string[] KeyNames =
    [
        "Enter", "Tab", "Space", "Backspace", "Delete", "Insert", "Home", "End", "PageUp", "PageDown",
        "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Plus", "Minus", "ContextMenu"
    ];

    private readonly List<DiagramCommand> commands = [];

    private readonly Func<IReadOnlyList<string>?, object?, DiagramCommandContext> createContext;

    internal DiagramCommandRegistry(Func<IReadOnlyList<string>?, object?, DiagramCommandContext> createContext)
    {
        this.createContext = createContext;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<DiagramCommand> All => commands;

    public DiagramCommand this[string name] => TryGet(name, out var command) ? command : throw new KeyNotFoundException($"The command '{name}' is not registered.");

    public bool TryGet(string name, [NotNullWhen(true)] out DiagramCommand? command)
    {
        command = commands.Find(x => String.Equals(x.Name, name, StringComparison.Ordinal));
        return command is not null;
    }

    public void Add(DiagramCommand command)
    {
        if (TryGet(command.Name, out var existing))
        {
            commands.Remove(existing);
        }

        var keys = command.Keys;
        command.ReplaceKeys([]);
        commands.Add(command);
        SetKeys(command.Name, keys);
        Refresh();
    }

    public bool Remove(string name)
    {
        if (!TryGet(name, out var command))
        {
            return false;
        }

        commands.Remove(command);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SetKeys(string name, IEnumerable<string> keys)
    {
        var command = this[name];
        string[] list = [.. keys.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase)];
        foreach (var other in commands)
        {
            if (!ReferenceEquals(other, command) && other.Keys.Any(x => list.Contains(x, StringComparer.OrdinalIgnoreCase)))
            {
                other.ReplaceKeys([.. other.Keys.Where(x => !list.Contains(x, StringComparer.OrdinalIgnoreCase))]);
            }
        }

        command.ReplaceKeys(list);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCanExecute(string name, Func<DiagramCommandContext, bool> canExecute)
    {
        this[name].AddCanExecute(canExecute);
        Refresh();
    }

    public void SetExecute(string name, Func<DiagramCommandContext, Task> execute)
    {
        this[name].Replacement = execute;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task ExecuteAsync(string name, object? argument = null) => ExecuteCoreAsync(name, null, argument);

    internal Task ExecuteForSelectionAsync(string name, IReadOnlyList<string> selection) => ExecuteCoreAsync(name, selection, null);

    private async Task ExecuteCoreAsync(string name, IReadOnlyList<string>? selection, object? argument)
    {
        if (!TryGet(name, out var command))
        {
            return;
        }

        var context = createContext(selection, argument);
        if (command.Evaluate(context))
        {
            await command.RunAsync(context).ConfigureAwait(true);
        }
    }

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    internal void Register(DiagramCommand command)
    {
        commands.Add(command);
        var keys = command.Keys;
        command.ReplaceKeys([]);
        SetKeys(command.Name, keys);
    }

    internal void Refresh()
    {
        var context = createContext(null, null);
        var changed = false;
        foreach (var command in commands)
        {
            var enabled = command.Evaluate(context);
            var isChecked = command.GetIsChecked?.Invoke(context) ?? false;
            if ((enabled != command.IsEnabled) || (isChecked != command.IsChecked))
            {
                command.IsEnabled = enabled;
                command.IsChecked = isChecked;
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string Normalize(string key)
    {
        var parts = key.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return key;
        }

        var (ctrl, alt, shift) = (false, false, false);
        var others = new List<string>();
        foreach (var modifier in parts.AsSpan(0, parts.Length - 1))
        {
            switch (modifier.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL" or "META" or "CMD":
                    ctrl = true;
                    break;
                case "ALT":
                    alt = true;
                    break;
                case "SHIFT":
                    shift = true;
                    break;
                default:
                    others.Add(modifier + "+");
                    break;
            }
        }

        return String.Concat(others) + (ctrl ? "Ctrl+" : string.Empty) + (alt ? "Alt+" : string.Empty) + (shift ? "Shift+" : string.Empty) + NameOf(parts[^1]);
    }

    private static string NameOf(string name)
    {
        if (name.Length == 1)
        {
            return name.ToUpperInvariant();
        }

        foreach (var known in KeyNames)
        {
            if (String.Equals(known, name, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return (name[0] == 'f') && !name.AsSpan(1).ContainsAnyExceptInRange('0', '9') ? String.Concat("F", name.AsSpan(1)) : name;
    }
}
