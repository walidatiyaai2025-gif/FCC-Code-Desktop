using System.Windows;
using System.Windows.Controls;
using FCCCodeDesktop.Fcc;

namespace FCCCodeDesktop.App.Shell;

public partial class ToolApprovalSurface : UserControl
{
    private TaskCompletionSource<bool>? _decision;
    public ToolApprovalSurface() { InitializeComponent(); Visibility = Visibility.Collapsed; }

    public async Task<bool> RequestAsync(FccToolPermissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Dispatcher.CheckAccess())
        {
            return await (await Dispatcher.InvokeAsync(() => RequestAsync(request, cancellationToken))).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (_decision is not null) { return false; }
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _decision = decision;
        RequestTitle.Text = $"{request.ToolName} · {request.WorkingDirectory}\nReview the exact operation. Approval applies only to this request. Sensitive values are redacted.";
        RequestInput.Text = request.InputJson;
        Visibility = Visibility.Visible;
        try { return await decision.Task.WaitAsync(cancellationToken).ConfigureAwait(true); }
        finally
        {
            Visibility = Visibility.Collapsed;
            _decision = null;
        }
    }

    private void OnAllowClick(object sender, RoutedEventArgs e) => _decision?.TrySetResult(true);
    private void OnDenyClick(object sender, RoutedEventArgs e) => _decision?.TrySetResult(false);
}
