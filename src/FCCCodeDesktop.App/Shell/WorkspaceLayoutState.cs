using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace FCCCodeDesktop.App.Shell;

/// <summary>
/// Presentation state for the resizable P02 workspace. Persistence is intentionally deferred to P03.
/// </summary>
public sealed class WorkspaceLayoutState : INotifyPropertyChanged
{
    public const double DefaultLeftPaneWidth = 240d;
    public const double DefaultRightPaneWidth = 300d;
    public const double MinimumSidePaneWidth = 160d;
    public const double MaximumSidePaneWidth = 480d;
    public const double DefaultBottomPanelHeight = 220d;
    public const double MinimumBottomPanelHeight = 120d;
    public const double MaximumBottomPanelHeight = 480d;
    public const double CollapsedBottomPanelHeight = 36d;

    private GridLength _leftPaneWidth = new(DefaultLeftPaneWidth);
    private GridLength _rightPaneWidth = new(DefaultRightPaneWidth);
    private GridLength _bottomPanelHeight = new(DefaultBottomPanelHeight);
    private double _lastExpandedLeftPaneWidth = DefaultLeftPaneWidth;
    private double _lastExpandedRightPaneWidth = DefaultRightPaneWidth;
    private double _lastExpandedBottomPanelHeight = DefaultBottomPanelHeight;
    private bool _isLeftPaneCollapsed;
    private bool _isRightPaneCollapsed;
    private bool _isBottomPanelCollapsed;

    public WorkspaceLayoutState()
    {
        ToggleBottomPanelCommand = new ToggleBottomPanelStateCommand(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ToggleBottomPanelCommand { get; }

    public GridLength LeftPaneWidth
    {
        get => _leftPaneWidth;
        set => SetPaneWidth(ref _leftPaneWidth, value, true);
    }

    public GridLength RightPaneWidth
    {
        get => _rightPaneWidth;
        set => SetPaneWidth(ref _rightPaneWidth, value, false);
    }

    public GridLength BottomPanelHeight
    {
        get => _bottomPanelHeight;
        set => SetBottomPanelHeight(value);
    }

    public bool IsLeftPaneCollapsed
    {
        get => _isLeftPaneCollapsed;
        private set => SetField(ref _isLeftPaneCollapsed, value);
    }

    public bool IsRightPaneCollapsed
    {
        get => _isRightPaneCollapsed;
        private set => SetField(ref _isRightPaneCollapsed, value);
    }

    public bool IsBottomPanelCollapsed
    {
        get => _isBottomPanelCollapsed;
        private set => SetField(ref _isBottomPanelCollapsed, value);
    }

    public void CollapseLeftPane()
    {
        if (!_leftPaneWidth.IsAbsolute || _leftPaneWidth.Value <= 0d)
        {
            return;
        }

        _lastExpandedLeftPaneWidth = ClampSidePaneWidth(_leftPaneWidth.Value);
        IsLeftPaneCollapsed = true;
        SetField(ref _leftPaneWidth, new GridLength(0d), nameof(LeftPaneWidth));
    }

    public void RestoreLeftPane()
    {
        IsLeftPaneCollapsed = false;
        SetField(ref _leftPaneWidth, new GridLength(ClampSidePaneWidth(_lastExpandedLeftPaneWidth)), nameof(LeftPaneWidth));
    }

    public void CollapseRightPane()
    {
        if (!_rightPaneWidth.IsAbsolute || _rightPaneWidth.Value <= 0d)
        {
            return;
        }

        _lastExpandedRightPaneWidth = ClampSidePaneWidth(_rightPaneWidth.Value);
        IsRightPaneCollapsed = true;
        SetField(ref _rightPaneWidth, new GridLength(0d), nameof(RightPaneWidth));
    }

    public void RestoreRightPane()
    {
        IsRightPaneCollapsed = false;
        SetField(ref _rightPaneWidth, new GridLength(ClampSidePaneWidth(_lastExpandedRightPaneWidth)), nameof(RightPaneWidth));
    }

    public void CollapseBottomPanel()
    {
        if (_bottomPanelHeight.IsAbsolute && _bottomPanelHeight.Value > CollapsedBottomPanelHeight)
        {
            _lastExpandedBottomPanelHeight = ClampBottomPanelHeight(_bottomPanelHeight.Value);
        }

        IsBottomPanelCollapsed = true;
        SetField(ref _bottomPanelHeight, new GridLength(CollapsedBottomPanelHeight), nameof(BottomPanelHeight));
    }

    public void RestoreBottomPanel()
    {
        IsBottomPanelCollapsed = false;
        SetField(
            ref _bottomPanelHeight,
            new GridLength(ClampBottomPanelHeight(_lastExpandedBottomPanelHeight)),
            nameof(BottomPanelHeight));
    }

    public void ToggleBottomPanel()
    {
        if (IsBottomPanelCollapsed)
        {
            RestoreBottomPanel();
            return;
        }

        CollapseBottomPanel();
    }

    public void Reset()
    {
        _lastExpandedLeftPaneWidth = DefaultLeftPaneWidth;
        _lastExpandedRightPaneWidth = DefaultRightPaneWidth;
        _lastExpandedBottomPanelHeight = DefaultBottomPanelHeight;
        IsLeftPaneCollapsed = false;
        IsRightPaneCollapsed = false;
        IsBottomPanelCollapsed = false;
        SetField(ref _leftPaneWidth, new GridLength(DefaultLeftPaneWidth), nameof(LeftPaneWidth));
        SetField(ref _rightPaneWidth, new GridLength(DefaultRightPaneWidth), nameof(RightPaneWidth));
        SetField(ref _bottomPanelHeight, new GridLength(DefaultBottomPanelHeight), nameof(BottomPanelHeight));
    }

    private void SetPaneWidth(ref GridLength field, GridLength value, bool isLeft)
    {
        if (!value.IsAbsolute)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Side-pane widths must be absolute GridLength values.");
        }

        if (value.Value <= 0d)
        {
            SetField(ref field, new GridLength(0d), isLeft ? nameof(LeftPaneWidth) : nameof(RightPaneWidth));
            if (isLeft)
            {
                IsLeftPaneCollapsed = true;
            }
            else
            {
                IsRightPaneCollapsed = true;
            }

            return;
        }

        var clamped = ClampSidePaneWidth(value.Value);
        if (isLeft)
        {
            _lastExpandedLeftPaneWidth = clamped;
            IsLeftPaneCollapsed = false;
        }
        else
        {
            _lastExpandedRightPaneWidth = clamped;
            IsRightPaneCollapsed = false;
        }

        SetField(ref field, new GridLength(clamped), isLeft ? nameof(LeftPaneWidth) : nameof(RightPaneWidth));
    }

    private void SetBottomPanelHeight(GridLength value)
    {
        if (!value.IsAbsolute)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Bottom-panel height must be an absolute GridLength value.");
        }

        if (value.Value <= CollapsedBottomPanelHeight)
        {
            CollapseBottomPanel();
            return;
        }

        var clamped = ClampBottomPanelHeight(value.Value);
        _lastExpandedBottomPanelHeight = clamped;
        IsBottomPanelCollapsed = false;
        SetField(ref _bottomPanelHeight, new GridLength(clamped), nameof(BottomPanelHeight));
    }

    private static double ClampSidePaneWidth(double value) =>
        Math.Clamp(value, MinimumSidePaneWidth, MaximumSidePaneWidth);

    private static double ClampBottomPanelHeight(double value) =>
        Math.Clamp(value, MinimumBottomPanelHeight, MaximumBottomPanelHeight);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private sealed class ToggleBottomPanelStateCommand : ICommand
    {
        private readonly WorkspaceLayoutState _owner;

        public ToggleBottomPanelStateCommand(WorkspaceLayoutState owner)
        {
            _owner = owner;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _owner.ToggleBottomPanel();
    }
}
