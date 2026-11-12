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

    public string? DisplayName { get; init; }

    public IReadOnlyList<string> Keys { get => keys; init => keys = value; }

    public bool IsEnabled { get; internal set; }

    public bool IsChecked { get; internal set; }

    public Func<DiagramCommandContext, bool>? CanExecute { get; init; }

    public Func<DiagramCommandContext, bool>? GetIsChecked { get; init; }

    public required Func<DiagramCommandContext, Task> Execute { get; init; }

    internal bool Local { get; init; }

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
    private readonly List<DiagramCommand> commands = [];

    private readonly Func<object?, DiagramCommandContext> createContext;

    internal DiagramCommandRegistry(Func<object?, DiagramCommandContext> createContext)
    {
        this.createContext = createContext;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<DiagramCommand> All => commands;

    public DiagramCommand this[string name] => TryGet(name, out var command) ? command : throw new KeyNotFoundException($"The command '{name}' is not registered.");

    public bool TryGet(string name, [NotNullWhen(true)] out DiagramCommand? command)
    {
        command = commands.Find(x => string.Equals(x.Name, name, StringComparison.Ordinal));
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

    public async Task ExecuteAsync(string name, object? argument = null)
    {
        if (!TryGet(name, out var command))
        {
            return;
        }

        var context = createContext(argument);
        if (command.Evaluate(context))
        {
            await command.RunAsync(context).ConfigureAwait(true);
        }
    }

    internal void Register(DiagramCommand command)
    {
        commands.Add(command);
        var keys = command.Keys;
        command.ReplaceKeys([]);
        SetKeys(command.Name, keys);
    }

    internal void Refresh()
    {
        var context = createContext(null);
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

        var name = parts[^1];
        var modifiers = parts[..^1];
        var builder = new List<string>(4);
        if (modifiers.Contains("Ctrl", StringComparer.OrdinalIgnoreCase))
        {
            builder.Add("Ctrl");
        }

        if (modifiers.Contains("Alt", StringComparer.OrdinalIgnoreCase))
        {
            builder.Add("Alt");
        }

        if (modifiers.Contains("Shift", StringComparer.OrdinalIgnoreCase))
        {
            builder.Add("Shift");
        }

        builder.Add(name.Length == 1 ? name.ToUpperInvariant() : name);
        return string.Join('+', builder);
    }
}
