using System.IO;
using System.Windows;
using System.Windows.Controls;
using FCCCodeDesktop.Core.State;
using Microsoft.Win32;

namespace FCCCodeDesktop.App.Projects;

public partial class ProjectWorkspaceSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ProjectWorkspaceState),
        typeof(ProjectWorkspaceSurface),
        new PropertyMetadata(null));

    public ProjectWorkspaceSurface()
    {
        InitializeComponent();
    }

    public ProjectWorkspaceState? State
    {
        get => (ProjectWorkspaceState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
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

        await ExecuteProjectActionAsync(() => state.OpenProjectAsync(dialog.FolderName, CancellationToken.None));
    }

    private async void OnOpenRecentProjectClick(object sender, RoutedEventArgs e)
    {
        if (State is not { IsBusy: false } state
            || sender is not Button { Tag: PersistedProject project })
        {
            return;
        }

        await ExecuteProjectActionAsync(() => state.OpenRecentProjectAsync(project, CancellationToken.None));
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (State is not { IsBusy: false } state)
        {
            return;
        }

        await ExecuteProjectActionAsync(async () =>
        {
            await state.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
            return state.ActiveProject ?? new PersistedProject(
                Guid.NewGuid(),
                Environment.CurrentDirectory,
                "Refresh",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
        });
    }

    private static async Task ExecuteProjectActionAsync(Func<Task<PersistedProject>> action)
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
