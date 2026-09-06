using System.IO;
using System.Windows;
using System.Windows.Controls;
using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.App.Editor;

public partial class ProjectEditorSurface : UserControl
{
    public static readonly DependencyProperty WorkspaceProperty = DependencyProperty.Register(
        nameof(Workspace),
        typeof(ProjectEditorWorkspace),
        typeof(ProjectEditorSurface),
        new PropertyMetadata(null));

    public ProjectEditorSurface()
    {
        InitializeComponent();
    }

    public ProjectEditorWorkspace? Workspace
    {
        get => (ProjectEditorWorkspace?)GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (Workspace is not { CanSave: true } workspace)
        {
            return;
        }

        try
        {
            await workspace.SaveSelectedAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is ProjectFileConflictException
                                           or IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            // ProjectEditorWorkspace exposes the actionable error and retains the dirty buffer.
        }
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        if (Workspace is not { CanReload: true, SelectedDocument: { } document } workspace)
        {
            return;
        }

        var discard = !document.IsDirty;
        if (!discard)
        {
            discard = MessageBox.Show(
                    Window.GetWindow(this),
                    $"Discard unsaved changes in {document.FileName} and reload from disk?",
                    "Reload file",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No)
                == MessageBoxResult.Yes;
        }

        if (!discard)
        {
            return;
        }

        try
        {
            await workspace.ReloadSelectedAsync(
                    discardUnsavedChanges: true,
                    CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is ProjectEditorOpenException
                                           or IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            // ProjectEditorWorkspace exposes the actionable error and keeps the current buffer.
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (Workspace is not { CanClose: true, SelectedDocument: { } document } workspace)
        {
            return;
        }

        var discard = !document.IsDirty;
        if (!discard)
        {
            discard = MessageBox.Show(
                    Window.GetWindow(this),
                    $"Discard unsaved changes in {document.FileName} and close the tab?",
                    "Close editor tab",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No)
                == MessageBoxResult.Yes;
        }

        if (!discard)
        {
            return;
        }

        workspace.CloseSelected(discardUnsavedChanges: true);
    }
}
