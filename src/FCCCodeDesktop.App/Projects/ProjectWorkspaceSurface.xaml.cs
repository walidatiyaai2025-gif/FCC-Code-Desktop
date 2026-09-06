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
        SearchState = new ProjectSearchState(new FileSystemProjectSearchService());
        InitializeComponent();
        AttachSearchSurface();
    }

    public ProjectWorkspaceState? State
    {
        get => (ProjectWorkspaceState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public ProjectFileExplorerState FileExplorerState { get; }
    public ProjectSearchState SearchState { get; }

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
            surface.SearchState.SetProject(newState.ActiveProject);
        }
        else
        {
            surface.FileExplorerState.SetProject(null);
            surface.SearchState.SetProject(null);
        }
    }

    private void OnProjectStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectWorkspaceState.ActiveProject) or null)
        {
            FileExplorerState.SetProject(State?.ActiveProject);
            SearchState.SetProject(State?.ActiveProject);
        }
    }

    private void AttachSearchSurface()
    {
        if (Content is not Grid workspaceGrid)
        {
            throw new InvalidOperationException("Project workspace root must remain a Grid so search can compose with the file surface.");
        }

        var contentGrid = workspaceGrid.Children
            .OfType<Grid>()
            .SingleOrDefault(child => Grid.GetRow(child) == 2)
            ?? throw new InvalidOperationException("Project workspace content grid was not found for search composition.");

        if (contentGrid.ColumnDefinitions.Count != 0)
        {
            throw new InvalidOperationException("Project workspace content grid already defines columns; search composition must be reconciled explicitly.");
        }

        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var searchSurface = new ProjectSearchSurface
        {
            State = SearchState,
            Margin = new Thickness(12, 0, 0, 0),
        };
        Grid.SetRow(searchSurface, 0);
        Grid.SetRowSpan(searchSurface, 2);
        Grid.SetColumn(searchSurface, 1);
        contentGrid.Children.Add(searchSurface);
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
