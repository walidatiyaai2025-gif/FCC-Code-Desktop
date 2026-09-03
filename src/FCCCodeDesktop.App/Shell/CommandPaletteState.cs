using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FCCCodeDesktop.App.Shell;

/// <summary>
/// Describes one shell command that can be discovered and executed through the command palette.
/// Later phases can register their own commands without coupling the palette to feature implementations.
/// </summary>
public sealed class ShellCommandDescriptor
{
    public ShellCommandDescriptor(
        string id,
        string title,
        string category,
        string? gestureText,
        ICommand command,
        object? commandParameter = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A non-empty command id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A non-empty command title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("A non-empty command category is required.", nameof(category));
        }

        ArgumentNullException.ThrowIfNull(command);

        Id = id.Trim();
        Title = title.Trim();
        Category = category.Trim();
        GestureText = string.IsNullOrWhiteSpace(gestureText) ? null : gestureText.Trim();
        Command = command;
        CommandParameter = commandParameter;
    }

    public string Id { get; }

    public string Title { get; }

    public string Category { get; }

    public string? GestureText { get; }

    public ICommand Command { get; }

    public object? CommandParameter { get; }

    public bool CanExecute() => Command.CanExecute(CommandParameter);

    public void Execute()
    {
        if (!CanExecute())
        {
            throw new InvalidOperationException($"Shell command '{Id}' cannot execute in the current state.");
        }

        Command.Execute(CommandParameter);
    }
}

/// <summary>
/// Presentation and registration state for the P02 command-palette and keyboard framework.
/// It intentionally owns no persistence or feature-specific implementation.
/// </summary>
public sealed class CommandPaletteState : INotifyPropertyChanged
{
    private readonly List<ShellCommandDescriptor> _registeredCommands = [];
    private readonly ObservableCollection<ShellCommandDescriptor> _filteredCommands = [];
    private readonly ReadOnlyObservableCollection<ShellCommandDescriptor> _readOnlyFilteredCommands;
    private readonly RelayCommand _dismissCommand;
    private readonly RelayCommand _moveSelectionCommand;
    private readonly RelayCommand _executeSelectedCommand;
    private bool _isOpen;
    private string _filterText = string.Empty;
    private int _selectedIndex = -1;

    public CommandPaletteState()
    {
        _readOnlyFilteredCommands = new ReadOnlyObservableCollection<ShellCommandDescriptor>(_filteredCommands);
        OpenCommand = new RelayCommand(_ => Open());
        _dismissCommand = new RelayCommand(_ => Close(), _ => IsOpen);
        _moveSelectionCommand = new RelayCommand(
            parameter => MoveSelection(ParseMoveOffset(parameter)),
            parameter => IsOpen && _filteredCommands.Count > 0 && TryParseMoveOffset(parameter, out _));
        _executeSelectedCommand = new RelayCommand(
            _ => ExecuteSelected(),
            _ => IsOpen && SelectedCommand?.CanExecute() == true);

        DismissCommand = _dismissCommand;
        MoveSelectionCommand = _moveSelectionCommand;
        ExecuteSelectedCommand = _executeSelectedCommand;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand OpenCommand { get; }

    public ICommand DismissCommand { get; }

    public ICommand MoveSelectionCommand { get; }

    public ICommand ExecuteSelectedCommand { get; }

    public IReadOnlyList<ShellCommandDescriptor> RegisteredCommands => _registeredCommands;

    public ReadOnlyObservableCollection<ShellCommandDescriptor> FilteredCommands => _readOnlyFilteredCommands;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (!SetField(ref _isOpen, value))
            {
                return;
            }

            RefreshCommandStates();
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            var normalized = value ?? string.Empty;
            if (!SetField(ref _filterText, normalized))
            {
                return;
            }

            RefreshFilteredCommands();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var normalized = NormalizeSelectedIndex(value);
            if (!SetField(ref _selectedIndex, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedCommand));
            RefreshCommandStates();
        }
    }

    public ShellCommandDescriptor? SelectedCommand =>
        SelectedIndex >= 0 && SelectedIndex < _filteredCommands.Count
            ? _filteredCommands[SelectedIndex]
            : null;

    public bool HasMatches => _filteredCommands.Count > 0;

    public void RegisterCommand(ShellCommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (_registeredCommands.Any(
                command => string.Equals(command.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A shell command with id '{descriptor.Id}' is already registered.");
        }

        _registeredCommands.Add(descriptor);
        RefreshFilteredCommands();
    }

    public bool UnregisterCommand(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A non-empty command id is required.", nameof(id));
        }

        var index = _registeredCommands.FindIndex(
            command => string.Equals(command.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        _registeredCommands.RemoveAt(index);
        RefreshFilteredCommands();
        return true;
    }

    public void Open()
    {
        if (_filterText.Length > 0)
        {
            _filterText = string.Empty;
            OnPropertyChanged(nameof(FilterText));
        }

        RefreshFilteredCommands();
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }

    public bool MoveSelection(int offset)
    {
        if (offset == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Selection movement must be non-zero.");
        }

        if (_filteredCommands.Count == 0)
        {
            SelectedIndex = -1;
            return false;
        }

        if (SelectedIndex < 0)
        {
            SelectedIndex = offset > 0 ? 0 : _filteredCommands.Count - 1;
            return true;
        }

        var next = (SelectedIndex + offset) % _filteredCommands.Count;
        if (next < 0)
        {
            next += _filteredCommands.Count;
        }

        SelectedIndex = next;
        return true;
    }

    public bool ExecuteSelected()
    {
        var selected = SelectedCommand;
        if (selected is null || !selected.CanExecute())
        {
            return false;
        }

        selected.Execute();
        Close();
        return true;
    }

    private void RefreshFilteredCommands()
    {
        var filter = FilterText.Trim();
        var matches = _registeredCommands.Where(command => MatchesFilter(command, filter)).ToArray();

        _filteredCommands.Clear();
        foreach (var command in matches)
        {
            _filteredCommands.Add(command);
        }

        OnPropertyChanged(nameof(HasMatches));
        SelectedIndex = _filteredCommands.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(SelectedCommand));
        RefreshCommandStates();
    }

    private static bool MatchesFilter(ShellCommandDescriptor command, string filter)
    {
        if (filter.Length == 0)
        {
            return true;
        }

        return command.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            command.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            command.Id.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private int NormalizeSelectedIndex(int value)
    {
        if (_filteredCommands.Count == 0)
        {
            return -1;
        }

        return Math.Clamp(value, 0, _filteredCommands.Count - 1);
    }

    private static int ParseMoveOffset(object? parameter)
    {
        if (!TryParseMoveOffset(parameter, out var offset))
        {
            throw new ArgumentException("A non-zero integer selection offset is required.", nameof(parameter));
        }

        return offset;
    }

    private static bool TryParseMoveOffset(object? parameter, out int offset)
    {
        switch (parameter)
        {
            case int integer when integer != 0:
                offset = integer;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                                  parsed != 0:
                offset = parsed;
                return true;
            default:
                offset = 0;
                return false;
        }
    }

    private void RefreshCommandStates()
    {
        _dismissCommand.RaiseCanExecuteChanged();
        _moveSelectionCommand.RaiseCanExecuteChanged();
        _executeSelectedCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            ArgumentNullException.ThrowIfNull(execute);
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
