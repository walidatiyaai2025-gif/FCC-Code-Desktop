using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.App.Projects;

public sealed record ProjectSearchModeOption(ProjectSearchMode Value, string Label);

public sealed class ProjectSearchState : DispatcherObject, INotifyPropertyChanged
{
    private static readonly ProjectSearchModeOption[] ModeOptions =
    [
        new(ProjectSearchMode.Content, "Content"),
        new(ProjectSearchMode.FileName, "File name"),
        new(ProjectSearchMode.RegularExpression, "Regex"),
    ];

    private readonly IProjectSearchService _searchService;
    private readonly ObservableCollection<ProjectSearchMatch> _matches = [];
    private readonly ReadOnlyObservableCollection<ProjectSearchMatch> _readonlyMatches;
    private PersistedProject? _project;
    private CancellationTokenSource? _searchCancellation;
    private int _projectGeneration;
    private string _query = string.Empty;
    private ProjectSearchMode _mode = ProjectSearchMode.Content;
    private bool _matchCase;
    private bool _isSearching;
    private string _statusText = "Open a project to search its workspace.";
    private string? _errorMessage;

    public ProjectSearchState(IProjectSearchService searchService)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _readonlyMatches = new ReadOnlyObservableCollection<ProjectSearchMatch>(_matches);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ProjectSearchModeOption> SearchModes => ModeOptions;
    public ReadOnlyObservableCollection<ProjectSearchMatch> Matches => _readonlyMatches;

    public string Query
    {
        get => _query;
        set
        {
            VerifyAccess();
            var normalizedValue = value ?? string.Empty;
            if (string.Equals(_query, normalizedValue, StringComparison.Ordinal))
            {
                return;
            }
            _query = normalizedValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSearch));
        }
    }

    public ProjectSearchMode Mode
    {
        get => _mode;
        set
        {
            VerifyAccess();
            if (_mode == value)
            {
                return;
            }
            _mode = value;
            OnPropertyChanged();
        }
    }

    public bool MatchCase
    {
        get => _matchCase;
        set
        {
            VerifyAccess();
            if (_matchCase == value)
            {
                return;
            }
            _matchCase = value;
            OnPropertyChanged();
        }
    }

    public bool HasProject => _project is not null;
    public bool IsSearching => _isSearching;
    public bool CanSearch => HasProject && !IsSearching && !string.IsNullOrWhiteSpace(Query);
    public bool CanCancel => IsSearching;
    public bool HasMatches => _matches.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string StatusText => _statusText;
    public string? ErrorMessage => _errorMessage;

    public void SetProject(PersistedProject? project)
    {
        VerifyAccess();
        if (_project?.Id == project?.Id
            && string.Equals(_project?.RootPath, project?.RootPath, StringComparison.Ordinal))
        {
            return;
        }

        _projectGeneration++;
        _searchCancellation?.Cancel();
        _project = project;
        _matches.Clear();
        SetErrorMessage(null);
        SetStatusText(project is null
            ? "Open a project to search its workspace."
            : "Search file names, text content, or line-based regular expressions.");
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(CanSearch));
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        if (IsSearching)
        {
            throw new InvalidOperationException("A workspace search is already running.");
        }

        var project = _project
            ?? throw new InvalidOperationException("Open a project before searching the workspace.");
        if (string.IsNullOrWhiteSpace(Query))
        {
            throw new ProjectSearchQueryException("Enter a search query.");
        }

        var generation = _projectGeneration;
        var request = new ProjectSearchRequest(project.RootPath, Query, Mode, MatchCase);
        var localCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _searchCancellation = localCancellation;
        _matches.Clear();
        OnPropertyChanged(nameof(HasMatches));
        SetErrorMessage(null);
        SetSearching(true);
        SetStatusText("Searching workspace…");

        try
        {
            var result = await _searchService.SearchAsync(request, localCancellation.Token).ConfigureAwait(true);
            VerifyAccess();
            if (generation != _projectGeneration)
            {
                return;
            }

            foreach (var match in result.Matches)
            {
                _matches.Add(match);
            }
            OnPropertyChanged(nameof(HasMatches));
            SetStatusText(BuildResultStatus(result));
        }
        catch (OperationCanceledException) when (localCancellation.IsCancellationRequested)
        {
            VerifyAccess();
            if (generation == _projectGeneration)
            {
                SetStatusText("Search cancelled. Existing source files were not modified.");
            }
        }
        catch (Exception exception) when (exception is ProjectSearchQueryException
                                           or DirectoryNotFoundException
                                           or UnauthorizedAccessException
                                           or IOException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            VerifyAccess();
            if (generation == _projectGeneration)
            {
                SetErrorMessage(exception.Message);
                SetStatusText("Workspace search failed.");
            }
            throw;
        }
        finally
        {
            VerifyAccess();
            if (ReferenceEquals(_searchCancellation, localCancellation))
            {
                _searchCancellation = null;
                SetSearching(false);
            }
            localCancellation.Dispose();
        }
    }

    public void CancelSearch()
    {
        VerifyAccess();
        _searchCancellation?.Cancel();
    }

    private static string BuildResultStatus(ProjectSearchResultSet result)
    {
        var noun = result.Matches.Count == 1 ? "match" : "matches";
        var limit = result.LimitReached
            ? " Search limits were reached; refine the query for more specific results."
            : string.Empty;
        return $"{result.Matches.Count} {noun} across {result.FilesExamined} examined files; "
               + $"{result.FilesSkipped} files and {result.DirectoriesSkipped} directories skipped.{limit}";
    }

    private void SetSearching(bool value)
    {
        if (_isSearching == value)
        {
            return;
        }
        _isSearching = value;
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanCancel));
    }

    private void SetStatusText(string value)
    {
        if (string.Equals(_statusText, value, StringComparison.Ordinal))
        {
            return;
        }
        _statusText = value;
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetErrorMessage(string? value)
    {
        if (string.Equals(_errorMessage, value, StringComparison.Ordinal))
        {
            return;
        }
        _errorMessage = value;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
