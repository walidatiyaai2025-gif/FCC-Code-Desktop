using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.App.Projects;

public partial class ProjectSearchSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ProjectSearchState),
        typeof(ProjectSearchSurface),
        new PropertyMetadata(null));

    public ProjectSearchSurface()
    {
        InitializeComponent();
    }

    public ProjectSearchState? State
    {
        get => (ProjectSearchState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) =>
        await ExecuteSearchAsync().ConfigureAwait(true);

    private void OnCancelSearchClick(object sender, RoutedEventArgs e) =>
        State?.CancelSearch();

    private async void OnSearchQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && State is { CanCancel: true } cancellingState)
        {
            cancellingState.CancelSearch();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || State is not { CanSearch: true })
        {
            return;
        }

        e.Handled = true;
        await ExecuteSearchAsync().ConfigureAwait(true);
    }

    private async Task ExecuteSearchAsync()
    {
        if (State is not { CanSearch: true } state)
        {
            return;
        }

        try
        {
            await state.SearchAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is ProjectSearchQueryException
                                           or DirectoryNotFoundException
                                           or UnauthorizedAccessException
                                           or IOException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            // ProjectSearchState records the actionable failure for inline presentation.
        }
    }
}
