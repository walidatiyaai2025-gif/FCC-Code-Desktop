using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FCCCodeDesktop.App.Shell;

public enum WorkspaceSection
{
    Projects,
    Sessions,
    Tasks,
}

public sealed class WorkspaceNavigationState : INotifyPropertyChanged
{
    private WorkspaceSection selectedSection = WorkspaceSection.Projects;
    private object? projectsContent;
    private object? sessionsContent;
    private object? tasksContent;

    public WorkspaceNavigationState()
    {
        SelectSectionCommand = new SelectWorkspaceSectionCommand(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SelectSectionCommand { get; }

    public WorkspaceSection SelectedSection
    {
        get => selectedSection;
        private set
        {
            if (selectedSection == value)
            {
                return;
            }

            selectedSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTitle));
            OnPropertyChanged(nameof(SelectedDescription));
            OnPropertyChanged(nameof(SelectedContent));
            OnPropertyChanged(nameof(IsProjectsSelected));
            OnPropertyChanged(nameof(IsSessionsSelected));
            OnPropertyChanged(nameof(IsTasksSelected));
        }
    }

    public string SelectedTitle => SelectedSection switch
    {
        WorkspaceSection.Projects => "Projects",
        WorkspaceSection.Sessions => "Sessions",
        WorkspaceSection.Tasks => "Tasks",
        _ => throw new InvalidOperationException($"Unsupported workspace section: {SelectedSection}"),
    };

    public string SelectedDescription => SelectedSection switch
    {
        WorkspaceSection.Projects => "Project navigation and workspace entry points.",
        WorkspaceSection.Sessions => "Agent session navigation and resume entry points.",
        WorkspaceSection.Tasks => "Task navigation and execution-state entry points.",
        _ => throw new InvalidOperationException($"Unsupported workspace section: {SelectedSection}"),
    };

    public object? SelectedContent => SelectedSection switch
    {
        WorkspaceSection.Projects => ProjectsContent,
        WorkspaceSection.Sessions => SessionsContent,
        WorkspaceSection.Tasks => TasksContent,
        _ => null,
    };

    public bool IsProjectsSelected => SelectedSection == WorkspaceSection.Projects;

    public bool IsSessionsSelected => SelectedSection == WorkspaceSection.Sessions;

    public bool IsTasksSelected => SelectedSection == WorkspaceSection.Tasks;

    public object? ProjectsContent
    {
        get => projectsContent;
        set => SetSectionContent(ref projectsContent, value, WorkspaceSection.Projects);
    }

    public object? SessionsContent
    {
        get => sessionsContent;
        set => SetSectionContent(ref sessionsContent, value, WorkspaceSection.Sessions);
    }

    public object? TasksContent
    {
        get => tasksContent;
        set => SetSectionContent(ref tasksContent, value, WorkspaceSection.Tasks);
    }

    public void SelectSection(WorkspaceSection section)
    {
        if (!Enum.IsDefined(section))
        {
            throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown workspace section.");
        }

        SelectedSection = section;
    }

    private void SetSectionContent(ref object? storage, object? value, WorkspaceSection section)
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

    private sealed class SelectWorkspaceSectionCommand : ICommand
    {
        private readonly WorkspaceNavigationState owner;

        public SelectWorkspaceSectionCommand(WorkspaceNavigationState owner)
        {
            this.owner = owner;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => parameter is WorkspaceSection;

        public void Execute(object? parameter)
        {
            if (parameter is not WorkspaceSection section)
            {
                throw new ArgumentException("A WorkspaceSection command parameter is required.", nameof(parameter));
            }

            owner.SelectSection(section);
        }
    }
}
