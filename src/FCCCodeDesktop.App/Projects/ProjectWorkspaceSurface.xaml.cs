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
