using System.Collections.ObjectModel;
using System.ComponentModel;
using FCCCodeDesktop.Fcc;

namespace FCCCodeDesktop.App.Shell;

public sealed class FccStartupState : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Message { get; private set; } = "Preparing FCC…";
    public bool CanRetry { get; private set; }
    private string? _selectedModel;
    private bool _isTaskActive;
    private bool _refreshingModels;
    public ObservableCollection<string> Models { get; } = [];
    public string? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (_isTaskActive || _refreshingModels || _selectedModel == value) { return; }
            _selectedModel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModel)));
        }
    }
    public bool CanSelectModel => !_isTaskActive && Models.Count > 0;
    public void SetTaskActive(bool active)
    {
        _isTaskActive = active;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSelectModel)));
    }
    public void SetModels(IReadOnlyList<string> models, string? preferredModel = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        if (_isTaskActive) { return; }
        var previous = Models.Count == 0 ? preferredModel ?? _selectedModel : _selectedModel;
        _refreshingModels = true;
        try
        {
            Models.Clear();
            foreach (var model in models) { Models.Add(model); }
            _selectedModel = previous is not null && Models.Contains(previous) ? previous : Models.FirstOrDefault();
        }
        finally { _refreshingModels = false; }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSelectModel)));
    }
    public void Update(FccServerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Message = status.Message;
        CanRetry = status.State is FccServerState.Failed or FccServerState.Ready;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRetry)));
    }
}
