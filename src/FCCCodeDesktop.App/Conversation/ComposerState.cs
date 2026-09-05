using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace FCCCodeDesktop.App.Conversation;

public enum ComposerContextKind
{
    Project,
    File,
    Selection,
    Reference,
}

public sealed record ComposerAttachmentSnapshot(string FullPath, string DisplayName, long SizeBytes);

public sealed record ComposerContextSnapshot(ComposerContextKind Kind, string Reference, string Label);

public sealed record ComposerSubmission(
    long SubmissionId,
    string Text,
    IReadOnlyList<ComposerAttachmentSnapshot> Attachments,
    IReadOnlyList<ComposerContextSnapshot> ContextReferences,
    DateTimeOffset CreatedUtc);

public sealed class ComposerSubmissionRequestedEventArgs : EventArgs
{
    public ComposerSubmissionRequestedEventArgs(ComposerSubmission submission)
    {
        Submission = submission ?? throw new ArgumentNullException(nameof(submission));
    }

    public ComposerSubmission Submission { get; }
}

public sealed class ComposerAttachmentState
{
    internal ComposerAttachmentState(string fullPath, string displayName, long sizeBytes)
    {
        FullPath = fullPath;
        DisplayName = displayName;
        SizeBytes = sizeBytes;
    }

    public string FullPath { get; }

    public string DisplayName { get; }

    public long SizeBytes { get; }

    public string SizeLabel => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024d:0.#} KB",
        _ => $"{SizeBytes / (1024d * 1024d):0.#} MB",
    };
}

public sealed class ComposerContextReferenceState
{
    internal ComposerContextReferenceState(ComposerContextKind kind, string reference, string label)
    {
        Kind = kind;
        Reference = reference;
        Label = label;
    }

    public ComposerContextKind Kind { get; }

    public string Reference { get; }

    public string Label { get; }

    public string KindLabel => Kind.ToString();
}

public sealed class ComposerState : DispatcherObject, INotifyPropertyChanged
{
    public const int MaxDraftLength = 12_000;
    public const int MaxAttachments = 8;
    public const int MaxContextReferences = 12;
    public const long MaxAttachmentBytes = 25L * 1024L * 1024L;

    private readonly ObservableCollection<ComposerAttachmentState> _attachments = [];
    private readonly ObservableCollection<ComposerContextReferenceState> _contextReferences = [];
    private readonly ReadOnlyObservableCollection<ComposerAttachmentState> _readonlyAttachments;
    private readonly ReadOnlyObservableCollection<ComposerContextReferenceState> _readonlyContextReferences;
    private readonly DelegateCommand _submitCommand;
    private readonly DelegateCommand _clearCommand;
    private string _draftText = string.Empty;
    private string? _validationMessage;
    private long _nextSubmissionId = 1;
    private long? _pendingSubmissionId;

    public ComposerState()
    {
        _readonlyAttachments = new ReadOnlyObservableCollection<ComposerAttachmentState>(_attachments);
        _readonlyContextReferences = new ReadOnlyObservableCollection<ComposerContextReferenceState>(_contextReferences);
        _submitCommand = new DelegateCommand(_ => RequestSubmission(), _ => CanSubmit);
        _clearCommand = new DelegateCommand(_ => Clear(), _ => HasDraftContent);
        RemoveAttachmentCommand = new DelegateCommand(
            parameter => RemoveAttachment(parameter as ComposerAttachmentState),
            parameter => parameter is ComposerAttachmentState);
        RemoveContextReferenceCommand = new DelegateCommand(
            parameter => RemoveContextReference(parameter as ComposerContextReferenceState),
            parameter => parameter is ComposerContextReferenceState);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<ComposerSubmissionRequestedEventArgs>? SubmissionRequested;

    public ReadOnlyObservableCollection<ComposerAttachmentState> Attachments => _readonlyAttachments;

    public ReadOnlyObservableCollection<ComposerContextReferenceState> ContextReferences => _readonlyContextReferences;

    public ICommand SubmitCommand => _submitCommand;

    public ICommand ClearCommand => _clearCommand;

    public ICommand RemoveAttachmentCommand { get; }

    public ICommand RemoveContextReferenceCommand { get; }

    public string DraftText
    {
        get => _draftText;
        set
        {
            value ??= string.Empty;
            if (value.Length > MaxDraftLength)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Composer text cannot exceed {MaxDraftLength} characters.");
            }

            if (string.Equals(_draftText, value, StringComparison.Ordinal))
            {
                return;
            }

            VerifyAccess();
            _draftText = value;
            SetValidationMessage(null);
            OnPropertyChanged();
            NotifyDerivedStateChanged();
        }
    }

    public string? ValidationMessage => _validationMessage;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(_validationMessage);

    public bool HasAttachments => _attachments.Count > 0;

    public bool HasContextReferences => _contextReferences.Count > 0;

    public bool HasDraftContent => !string.IsNullOrWhiteSpace(DraftText) || HasAttachments || HasContextReferences;

    public bool CanSubmit => _pendingSubmissionId is null && !string.IsNullOrWhiteSpace(DraftText);

    public int DraftCharacterCount => DraftText.Length;

    public bool TryAddAttachment(string path)
    {
        VerifyAccess();

        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Attachment path is required.", nameof(path));
            }

            if (_attachments.Count >= MaxAttachments)
            {
                throw new InvalidOperationException($"A message can include at most {MaxAttachments} attachments.");
            }

            var fullPath = Path.GetFullPath(path.Trim());
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Attachment file does not exist or is not accessible.", fullPath);
            }

            if (_attachments.Any(item => string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("This file is already attached.");
            }

            var file = new FileInfo(fullPath);
            if (file.Length > MaxAttachmentBytes)
            {
                throw new InvalidOperationException("Attachment exceeds the 25 MB composer limit.");
            }

            _attachments.Add(new ComposerAttachmentState(fullPath, file.Name, file.Length));
            SetValidationMessage(null);
            OnPropertyChanged(nameof(HasAttachments));
            NotifyDerivedStateChanged();
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or IOException
                                           or UnauthorizedAccessException
                                           or NotSupportedException
                                           or InvalidOperationException)
        {
            SetValidationMessage(exception.Message);
            return false;
        }
    }

    public bool TryAddContextReference(ComposerContextKind kind, string reference, string label)
    {
        VerifyAccess();

        if (!Enum.IsDefined(kind))
        {
            SetValidationMessage("Context kind is not supported.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            SetValidationMessage("Context reference is required.");
            return false;
        }

        if (_contextReferences.Count >= MaxContextReferences)
        {
            SetValidationMessage($"A message can include at most {MaxContextReferences} context references.");
            return false;
        }

        var normalizedReference = reference.Trim();
        if (_contextReferences.Any(item => item.Kind == kind
                                           && string.Equals(item.Reference, normalizedReference, StringComparison.OrdinalIgnoreCase)))
        {
            SetValidationMessage("This context reference is already included.");
            return false;
        }

        var normalizedLabel = string.IsNullOrWhiteSpace(label) ? normalizedReference : label.Trim();
        _contextReferences.Add(new ComposerContextReferenceState(kind, normalizedReference, normalizedLabel));
        SetValidationMessage(null);
        OnPropertyChanged(nameof(HasContextReferences));
        NotifyDerivedStateChanged();
        return true;
    }

    public ComposerSubmission CreateSubmission()
    {
        VerifyAccess();

        var text = DraftText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Enter a message before submitting the composer.");
        }

        return new ComposerSubmission(
            _nextSubmissionId,
            text,
            _attachments.Select(item => new ComposerAttachmentSnapshot(item.FullPath, item.DisplayName, item.SizeBytes)).ToArray(),
            _contextReferences.Select(item => new ComposerContextSnapshot(item.Kind, item.Reference, item.Label)).ToArray(),
            DateTimeOffset.UtcNow);
    }

    public bool RequestSubmission()
    {
        VerifyAccess();

        if (!CanSubmit)
        {
            SetValidationMessage(_pendingSubmissionId is null
                ? "Enter a message before submitting the composer."
                : "The previous composer submission is still being processed.");
            return false;
        }

        if (SubmissionRequested is null)
        {
            SetValidationMessage("No conversation submission handler is available.");
            return false;
        }

        var submission = CreateSubmission();
        _pendingSubmissionId = submission.SubmissionId;
        _nextSubmissionId = checked(_nextSubmissionId + 1);
        NotifyDerivedStateChanged();

        try
        {
            SubmissionRequested.Invoke(this, new ComposerSubmissionRequestedEventArgs(submission));
            return true;
        }
        catch
        {
            _pendingSubmissionId = null;
            NotifyDerivedStateChanged();
            throw;
        }
    }

    public void AcceptSubmission(long submissionId)
    {
        VerifyAccess();

        if (_pendingSubmissionId != submissionId)
        {
            throw new InvalidOperationException("Composer submission identity does not match the pending submission.");
        }

        _pendingSubmissionId = null;
        ClearCore();
    }

    public void RejectSubmission(long submissionId, string message)
    {
        VerifyAccess();

        if (_pendingSubmissionId != submissionId)
        {
            throw new InvalidOperationException("Composer submission identity does not match the pending submission.");
        }

        _pendingSubmissionId = null;
        SetValidationMessage(string.IsNullOrWhiteSpace(message) ? "Composer submission was rejected." : message.Trim());
        NotifyDerivedStateChanged();
    }

    public void Clear()
    {
        VerifyAccess();

        if (_pendingSubmissionId is not null)
        {
            SetValidationMessage("The composer cannot be cleared while a submission is being processed.");
            return;
        }

        ClearCore();
    }

    private void RemoveAttachment(ComposerAttachmentState? attachment)
    {
        VerifyAccess();
        if (attachment is null || !_attachments.Remove(attachment))
        {
            return;
        }

        SetValidationMessage(null);
        OnPropertyChanged(nameof(HasAttachments));
        NotifyDerivedStateChanged();
    }

    private void RemoveContextReference(ComposerContextReferenceState? contextReference)
    {
        VerifyAccess();
        if (contextReference is null || !_contextReferences.Remove(contextReference))
        {
            return;
        }

        SetValidationMessage(null);
        OnPropertyChanged(nameof(HasContextReferences));
        NotifyDerivedStateChanged();
    }

    private void ClearCore()
    {
        _draftText = string.Empty;
        _attachments.Clear();
        _contextReferences.Clear();
        SetValidationMessage(null);
        OnPropertyChanged(nameof(DraftText));
        OnPropertyChanged(nameof(HasAttachments));
        OnPropertyChanged(nameof(HasContextReferences));
        NotifyDerivedStateChanged();
    }

    private void NotifyDerivedStateChanged()
    {
        OnPropertyChanged(nameof(HasDraftContent));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(DraftCharacterCount));
        _submitCommand.RaiseCanExecuteChanged();
        _clearCommand.RaiseCanExecuteChanged();
    }

    private void SetValidationMessage(string? message)
    {
        var normalized = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (string.Equals(_validationMessage, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _validationMessage = normalized;
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationMessage));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?> _canExecute;

        public DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute(parameter);

        public void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _execute(parameter);
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
