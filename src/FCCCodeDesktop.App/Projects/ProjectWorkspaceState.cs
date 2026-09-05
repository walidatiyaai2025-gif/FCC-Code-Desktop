using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.App.Projects;

public sealed class ProjectWorkspaceState : DispatcherObject, INotifyPropertyChanged
{
    private readonly ProjectCatalogService _catalog;
    private readonly IProjectTechnologyDetectionService _technologyDetection;
    private readonly SessionWorkspaceState _sessions;
    private readonly ObservableCollection<PersistedProject> _recentProjects = [];
    private readonly ReadOnlyObservableCollection<PersistedProject> _readonlyRecentProjects;
    private readonly ObservableCollection<ProjectTechnologyDetection> _detectedTechnologies = [];
    private readonly ReadOnlyObservableCollection<ProjectTechnologyDetection> _readonlyDetectedTechnologies;
    private PersistedProject? _activeProject;
    private ProjectTechnologyScanResult? _technologyScan;
    private bool _isBusy;
    private string? _errorMessage;

    public ProjectWorkspaceState(
        ProjectCatalogService catalog,
        IProjectTechnologyDetectionService technologyDetection,
        SessionWorkspaceState sessions)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _technologyDetection = technologyDetection ?? throw new ArgumentNullException(nameof(technologyDetection));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _readonlyRecentProjects = new ReadOnlyObservableCollection<PersistedProject>(_recentProjects);
        _readonlyDetectedTechnologies = new ReadOnlyObservableCollection<ProjectTechnologyDetection>(_detectedTechnologies);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<PersistedProject> RecentProjects => _readonlyRecentProjects;

    public ReadOnlyObservableCollection<ProjectTechnologyDetection> DetectedTechnologies => _readonlyDetectedTechnologies;

    public PersistedProject? ActiveProject => _activeProject;

    public string ActiveProjectName => ActiveProject?.DisplayName ?? "No project open";

    public string ActiveProjectPath => ActiveProject?.RootPath ?? "Choose an existing folder to start a workspace.";

    public bool HasActiveProject => ActiveProject is not null;

    public bool HasRecentProjects => _recentProjects.Count > 0;

    public bool HasDetectedTechnologies => _detectedTechnologies.Count > 0;

    public bool IsBusy => _isBusy;

    public bool CanOpenProject => !IsBusy;

    public bool CanRescanTechnologies => HasActiveProject && !IsBusy;

    public string? ErrorMessage => _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string TechnologySummary => ActiveProject is null
        ? "Technology detection starts after a project is opened."
        : _technologyScan is null
            ? "Technology markers have not been scanned yet."
            : _technologyScan.HasDetections
                ? $"Detected {_technologyScan.Technologies.Count} project technolog{(_technologyScan.Technologies.Count == 1 ? "y" : "ies")}."
                : "No known project technology markers were detected within the bounded scan.";

    public string TechnologyScanDetail => _technologyScan is null
        ? string.Empty
        : $"Examined {_technologyScan.EntriesExamined} entries to depth {_technologyScan.MaximumDepth} "
          + $"with cap {_technologyScan.MaximumEntries}; skipped {_technologyScan.SkippedPaths} paths"
          + (_technologyScan.LimitReached ? "; entry cap reached." : ".");

    public string StatusText => IsBusy
        ? "Updating project workspace…"
        : HasActiveProject
            ? $"Active project: {ActiveProjectName}"
            : HasRecentProjects
                ? "Choose a recent project or open another folder."
                : "Open a project folder to add it to your local workspace history.";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        await ExecuteBusyAsync(
            async () =>
            {
                var projects = await _catalog
                    .ListRecentProjectsAsync(ProjectCatalogService.DefaultRecentProjectCount, cancellationToken)
                    .ConfigureAwait(true);
                ReplaceRecentProjects(projects);
            },
            cancellationToken).ConfigureAwait(true);
    }

    public async Task<PersistedProject> OpenProjectAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        PersistedProject? openedProject = null;
        await ExecuteBusyAsync(
            async () =>
            {
                openedProject = await _catalog.OpenProjectAsync(rootPath, cancellationToken).ConfigureAwait(true);
                await _sessions.ActivateProjectAsync(openedProject.Id, cancellationToken).ConfigureAwait(true);
                _activeProject = openedProject;
                NotifyActiveProjectChanged();
                await RefreshTechnologyDetectionCoreAsync(openedProject.RootPath, cancellationToken).ConfigureAwait(true);
                var projects = await _catalog
                    .ListRecentProjectsAsync(ProjectCatalogService.DefaultRecentProjectCount, cancellationToken)
                    .ConfigureAwait(true);
                ReplaceRecentProjects(projects);
            },
            cancellationToken).ConfigureAwait(true);

        return openedProject
            ?? throw new InvalidOperationException("Project open completed without producing a persisted project.");
    }

    public Task<PersistedProject> OpenRecentProjectAsync(
        PersistedProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        return OpenProjectAsync(project.RootPath, cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        await ExecuteBusyAsync(
            async () =>
            {
                var projects = await _catalog
                    .ListRecentProjectsAsync(ProjectCatalogService.DefaultRecentProjectCount, cancellationToken)
                    .ConfigureAwait(true);
                ReplaceRecentProjects(projects);
            },
            cancellationToken).ConfigureAwait(true);
    }

    public async Task RefreshTechnologyDetectionAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        var activeProject = ActiveProject
            ?? throw new InvalidOperationException("Open a project before rescanning technology markers.");
        await ExecuteBusyAsync(
            () => RefreshTechnologyDetectionCoreAsync(activeProject.RootPath, cancellationToken),
            cancellationToken).ConfigureAwait(true);
    }

    private async Task RefreshTechnologyDetectionCoreAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var scan = await _technologyDetection.DetectAsync(rootPath, cancellationToken).ConfigureAwait(true);
        VerifyAccess();
        _technologyScan = scan;
        _detectedTechnologies.Clear();
        foreach (var technology in scan.Technologies)
        {
            _detectedTechnologies.Add(technology);
        }

        OnPropertyChanged(nameof(DetectedTechnologies));
        OnPropertyChanged(nameof(HasDetectedTechnologies));
        OnPropertyChanged(nameof(TechnologySummary));
        OnPropertyChanged(nameof(TechnologyScanDetail));
    }

    private async Task ExecuteBusyAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("A project workspace operation is already running.");
        }

        SetBusy(true);
        SetErrorMessage(null);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetErrorMessage(exception.Message);
            throw;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ReplaceRecentProjects(IReadOnlyList<PersistedProject> projects)
    {
        VerifyAccess();
        _recentProjects.Clear();
        foreach (var project in projects)
        {
            _recentProjects.Add(project);
        }

        OnPropertyChanged(nameof(HasRecentProjects));
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetBusy(bool value)
    {
        VerifyAccess();
        if (_isBusy == value)
        {
            return;
        }

        _isBusy = value;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanOpenProject));
        OnPropertyChanged(nameof(CanRescanTechnologies));
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetErrorMessage(string? value)
    {
        VerifyAccess();
        if (string.Equals(_errorMessage, value, StringComparison.Ordinal))
        {
            return;
        }

        _errorMessage = value;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private void NotifyActiveProjectChanged()
    {
        OnPropertyChanged(nameof(ActiveProject));
        OnPropertyChanged(nameof(ActiveProjectName));
        OnPropertyChanged(nameof(ActiveProjectPath));
        OnPropertyChanged(nameof(HasActiveProject));
        OnPropertyChanged(nameof(CanRescanTechnologies));
        OnPropertyChanged(nameof(TechnologySummary));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
