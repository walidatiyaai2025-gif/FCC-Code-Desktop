using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.App.Projects;

public sealed class ProjectFileExplorerState : DispatcherObject, INotifyPropertyChanged
{
    private readonly IProjectFileExplorerService _fileExplorer;
    private readonly ObservableCollection<ProjectFileTreeNode> _roots = [];
    private readonly ReadOnlyObservableCollection<ProjectFileTreeNode> _readonlyRoots;
    private PersistedProject? _project;

    public ProjectFileExplorerState(IProjectFileExplorerService fileExplorer)
    {
        _fileExplorer = fileExplorer ?? throw new ArgumentNullException(nameof(fileExplorer));
        _readonlyRoots = new ReadOnlyObservableCollection<ProjectFileTreeNode>(_roots);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ProjectFileTreeNode> Roots => _readonlyRoots;

    public bool HasProject => _project is not null;

    public string StatusText => _project is null
        ? "Open a project to browse its files."
        : "Folders load only when expanded. Reparse-point directories are visible but never traversed.";

    public void SetProject(PersistedProject? project)
    {
        VerifyAccess();
        if (_project?.Id == project?.Id
            && string.Equals(_project?.RootPath, project?.RootPath, StringComparison.Ordinal))
        {
            return;
        }

        _project = project;
        RebuildRoot();
    }

    public void Refresh()
    {
        VerifyAccess();
        RebuildRoot();
    }

    public async Task LoadChildrenAsync(
        ProjectFileTreeNode node,
        CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(node);
        var project = _project
            ?? throw new InvalidOperationException("Open a project before browsing files.");

        if (!node.CanExpand || node.ChildrenLoaded || node.IsLoading)
        {
            return;
        }

        node.BeginLoading();
        try
        {
            var listing = await _fileExplorer
                .ListChildrenAsync(project.RootPath, node.FullPath, cancellationToken)
                .ConfigureAwait(true);
            VerifyAccess();
            node.CompleteLoading(listing);
        }
        catch (OperationCanceledException)
        {
            node.FailLoading("Directory loading was cancelled. Refresh the tree to try again.");
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            node.FailLoading(exception.Message);
            throw;
        }
    }

    private void RebuildRoot()
    {
        VerifyAccess();
        _roots.Clear();
        if (_project is not null)
        {
            _roots.Add(ProjectFileTreeNode.CreateRoot(_project.RootPath, _project.DisplayName));
        }

        OnPropertyChanged(nameof(Roots));
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
