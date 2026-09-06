using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Files;
using Microsoft.Win32;

namespace FCCCodeDesktop.App.Projects;

public partial class ProjectWorkspaceSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ProjectWorkspaceState),
        typeof(ProjectWorkspaceSurface),
        new PropertyMetadata(null, OnStateChanged));

    public ProjectWorkspaceSurface()
    {
        FileExplorerState = new ProjectFileExplorerState(new FileSystemProjectFileExplorerService());
        InitializeComponent();
    }

    public ProjectWorkspaceState? State
    {
        get => (ProjectWorkspaceState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public ProjectFileExplorerState FileExplorerState { get; }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var surface = (ProjectWorkspaceSurface)dependencyObject;
        if (e.OldValue is ProjectWorkspaceState oldState)
        {
            oldState.PropertyChanged -= surface.OnProjectStatePropertyChanged;
        }

        if (e.NewValue is ProjectWorkspaceState newState)
        {
            newState.PropertyChanged += surface.OnProjectStatePropertyChanged;
            surface.FileExplorerState.SetProject(newState.ActiveProject);
        }
        else
        {
            surface.FileExplorerState.SetProject(null);
        }
    }

    private void OnProjectStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectWorkspaceState.ActiveProject) or null)
        {
            FileExplorerState.SetProject(State?.ActiveProject);
        }
    }

    private async void OnOpenProjectClick(object sender, RoutedEventArgs e)
    {
        var state = State;
        if (state is null || state.IsBusy)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Open project folder",
            Multiselect = false,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        await ExecuteProjectActionAsync(
            async () =>
            {
                _ = await state.OpenProjectAsync(dialog.FolderName, CancellationToken.None).ConfigureAwait(true);
            });
    }

    private async void OnOpenRecentProjectClick(object sender, RoutedEventArgs e)
    {
        if (State is not { IsBusy: false } state
            || sender is not Button { Tag: PersistedProject project })
        {
            return;
        }

        await ExecuteProjectActionAsync(
            async () =>
            {
                _ = await state.OpenRecentProjectAsync(project, CancellationToken.None).ConfigureAwait(true);
            });
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (State is not { IsBusy: false } state)
        {
            return;
        }

        await ExecuteProjectActionAsync(() => state.RefreshAsync(CancellationToken.None));
    }

    private async void OnRescanTechnologiesClick(object sender, RoutedEventArgs e)
    {
        if (State is not { CanRescanTechnologies: true } state)
        {
            return;
        }

        await ExecuteProjectActionAsync(() => state.RefreshTechnologyDetectionAsync(CancellationToken.None));
    }

    private void OnRefreshFileExplorerClick(object sender, RoutedEventArgs e) =>
        FileExplorerState.Refresh();

    private async void OnFileExplorerNodeExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem { DataContext: ProjectFileTreeNode node } item
            || !ReferenceEquals(e.OriginalSource, item)
            || !node.CanExpand
            || node.ChildrenLoaded
            || node.IsLoading)
        {
            return;
        }

        try
        {
            await FileExplorerState.LoadChildrenAsync(node, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            // ProjectFileExplorerState records the actionable message inline on the affected node.
        }
    }

    private static async Task ExecuteProjectActionAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            // ProjectWorkspaceState already records the actionable message for inline presentation.
        }
    }
}
