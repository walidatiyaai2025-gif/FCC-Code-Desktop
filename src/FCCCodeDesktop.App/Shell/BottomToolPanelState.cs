using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FCCCodeDesktop.App.Shell;

public enum BottomToolSection
{
    Output,
    Problems,
    Terminal,
}

/// <summary>
/// Presentation-only selection/content state for the P02 bottom tool-panel framework.
/// Later phases supply production output/problem/terminal content through these seams.
/// </summary>
public sealed class BottomToolPanelState : INotifyPropertyChanged
{
    private BottomToolSection _selectedSection = BottomToolSection.Output;
    private object? _outputContent;
    private object? _problemsContent;
    private object? _terminalContent;

    public BottomToolPanelState()
    {
        SelectSectionCommand = new SelectBottomToolSectionCommand(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SelectSectionCommand { get; }

    public BottomToolSection SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (_selectedSection == value)
            {
                return;
            }

            _selectedSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedContent));
            OnPropertyChanged(nameof(IsOutputSelected));
            OnPropertyChanged(nameof(IsProblemsSelected));
            OnPropertyChanged(nameof(IsTerminalSelected));
        }
    }

    public object? SelectedContent => SelectedSection switch
    {
        BottomToolSection.Output => OutputContent,
        BottomToolSection.Problems => ProblemsContent,
        BottomToolSection.Terminal => TerminalContent,
        _ => null,
    };

    public bool IsOutputSelected => SelectedSection == BottomToolSection.Output;

    public bool IsProblemsSelected => SelectedSection == BottomToolSection.Problems;

    public bool IsTerminalSelected => SelectedSection == BottomToolSection.Terminal;

    public object? OutputContent
    {
        get => _outputContent;
        set => SetSectionContent(ref _outputContent, value, BottomToolSection.Output);
    }

    public object? ProblemsContent
    {
        get => _problemsContent;
        set => SetSectionContent(ref _problemsContent, value, BottomToolSection.Problems);
    }

    public object? TerminalContent
    {
        get => _terminalContent;
        set => SetSectionContent(ref _terminalContent, value, BottomToolSection.Terminal);
    }

    public void SelectSection(BottomToolSection section)
    {
        if (!Enum.IsDefined(section))
        {
            throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown bottom tool-panel section.");
        }

        SelectedSection = section;
    }

    private void SetSectionContent(ref object? storage, object? value, BottomToolSection section)
    {
        if (ReferenceEquals(storage, value))
        {
            return;
        }

        storage = value;
        if (SelectedSection == section)
        {
            OnPropertyChanged(nameof(SelectedContent));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class SelectBottomToolSectionCommand : ICommand
    {
        private readonly BottomToolPanelState _owner;

        public SelectBottomToolSectionCommand(BottomToolPanelState owner)
        {
            _owner = owner;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is BottomToolSection;

        public void Execute(object? parameter)
        {
            if (parameter is not BottomToolSection section)
            {
                throw new ArgumentException("A BottomToolSection command parameter is required.", nameof(parameter));
            }

            _owner.SelectSection(section);
        }
    }
}
