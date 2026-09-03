namespace FCCCodeDesktop.App.Shell;

public enum WorkspaceViewportProfile
{
    Compact,
    Standard,
    Wide
}

/// <summary>
/// Applies P02 responsive shell behavior using WPF device-independent pixels.
/// Persistence of user layout preferences remains deferred to P03.
/// </summary>
public sealed class WorkspaceViewportCoordinator
{
    public const double CompactWidthThreshold = 800d;
    public const double WideWidthThreshold = 1180d;
    public const double CompactHeightThreshold = 560d;

    private bool _leftPaneForcedCollapsed;
    private bool _rightPaneForcedCollapsed;
    private bool _bottomPanelForcedCollapsed;

    public WorkspaceViewportProfile Profile { get; private set; } = WorkspaceViewportProfile.Wide;

    public double LastWidthDip { get; private set; }

    public double LastHeightDip { get; private set; }

    public double DpiScaleX { get; private set; } = 1d;

    public double DpiScaleY { get; private set; } = 1d;

    public void Update(
        WorkspaceLayoutState state,
        double widthDip,
        double heightDip,
        double dpiScaleX,
        double dpiScaleY)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidatePositiveFinite(widthDip, nameof(widthDip));
        ValidatePositiveFinite(heightDip, nameof(heightDip));
        ValidatePositiveFinite(dpiScaleX, nameof(dpiScaleX));
        ValidatePositiveFinite(dpiScaleY, nameof(dpiScaleY));

        LastWidthDip = widthDip;
        LastHeightDip = heightDip;
        DpiScaleX = dpiScaleX;
        DpiScaleY = dpiScaleY;

        Profile = widthDip switch
        {
            < CompactWidthThreshold => WorkspaceViewportProfile.Compact,
            < WideWidthThreshold => WorkspaceViewportProfile.Standard,
            _ => WorkspaceViewportProfile.Wide
        };

        ApplyHorizontalProfile(state, Profile);
        ApplyVerticalProfile(state, heightDip);
    }

    private void ApplyHorizontalProfile(WorkspaceLayoutState state, WorkspaceViewportProfile profile)
    {
        switch (profile)
        {
            case WorkspaceViewportProfile.Compact:
                ForceCollapseLeftPane(state);
                ForceCollapseRightPane(state);
                break;
            case WorkspaceViewportProfile.Standard:
                RestoreForcedLeftPane(state);
                ForceCollapseRightPane(state);
                break;
            case WorkspaceViewportProfile.Wide:
                RestoreForcedLeftPane(state);
                RestoreForcedRightPane(state);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown workspace viewport profile.");
        }
    }

    private void ApplyVerticalProfile(WorkspaceLayoutState state, double heightDip)
    {
        if (heightDip < CompactHeightThreshold)
        {
            if (!state.IsBottomPanelCollapsed)
            {
                state.CollapseBottomPanel();
                _bottomPanelForcedCollapsed = true;
            }

            return;
        }

        if (_bottomPanelForcedCollapsed)
        {
            state.RestoreBottomPanel();
            _bottomPanelForcedCollapsed = false;
        }
    }

    private void ForceCollapseLeftPane(WorkspaceLayoutState state)
    {
        if (state.IsLeftPaneCollapsed)
        {
            return;
        }

        state.CollapseLeftPane();
        _leftPaneForcedCollapsed = true;
    }

    private void ForceCollapseRightPane(WorkspaceLayoutState state)
    {
        if (state.IsRightPaneCollapsed)
        {
            return;
        }

        state.CollapseRightPane();
        _rightPaneForcedCollapsed = true;
    }

    private void RestoreForcedLeftPane(WorkspaceLayoutState state)
    {
        if (!_leftPaneForcedCollapsed)
        {
            return;
        }

        state.RestoreLeftPane();
        _leftPaneForcedCollapsed = false;
    }

    private void RestoreForcedRightPane(WorkspaceLayoutState state)
    {
        if (!_rightPaneForcedCollapsed)
        {
            return;
        }

        state.RestoreRightPane();
        _rightPaneForcedCollapsed = false;
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Viewport and DPI values must be positive finite numbers.");
        }
    }
}
