using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FCCCodeDesktop.Application.Projects;

public sealed class ProjectEditorWorkspace : INotifyPropertyChanged
{
    private readonly IProjectFileService _fileService;
    private readonly ObservableCollection<ProjectEditorDocument> _documents = [];
    private readonly ReadOnlyObservableCollection<ProjectEditorDocument> _readonlyDocuments;
    private ProjectEditorDocument? _selectedDocument;
    private string? _activeProjectRootPath;
    private string _statusText = "Select a text file from the project tree to open it.";
    private string? _errorMessage;
    private bool _isBusy;

    public ProjectEditorWorkspace(IProjectFileService fileService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _readonlyDocuments = new ReadOnlyObservableCollection<ProjectEditorDocument>(_documents);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ProjectEditorDocument> Documents => _readonlyDocuments;

    public ProjectEditorDocument? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (ReferenceEquals(_selectedDocument, value))
            {
                return;
            }

            _selectedDocument = value;
            OnPropertyChanged();
            RaiseCommandStateChanged();
        }
    }

    public string? ActiveProjectRootPath => _activeProjectRootPath;
    public bool HasDocuments => _documents.Count > 0;
    public bool IsBusy => _isBusy;
    public bool CanSave => !IsBusy && SelectedDocument is { IsDirty: true };
    public bool CanReload => !IsBusy && SelectedDocument is not null;
    public bool CanClose => !IsBusy && SelectedDocument is not null;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string StatusText => _statusText;
    public string? ErrorMessage => _errorMessage;

    public void SetActiveProject(string? projectRootPath)
    {
        var normalized = string.IsNullOrWhiteSpace(projectRootPath)
            ? null
            : Path.GetFullPath(projectRootPath);
        if (string.Equals(_activeProjectRootPath, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _activeProjectRootPath = normalized;
        SetError(null);
        SetStatus(normalized is null
            ? "Open a project to edit files. Existing tabs remain attached to their original project roots."
            : "Select a text file from the project tree to open it. Existing tabs save only to their original project roots.");
        OnPropertyChanged(nameof(ActiveProjectRootPath));
    }

    public async Task<ProjectEditorDocument> OpenAsync(
        string projectRootPath,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedRoot = Path.GetFullPath(projectRootPath);
        var normalizedPath = Path.GetFullPath(filePath);
        var existing = _documents.FirstOrDefault(document =>
            string.Equals(document.ProjectRootPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            && string.Equals(document.FullPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedDocument = existing;
            SetError(null);
            SetStatus($"{existing.RelativePath} is already open.");
            return existing;
        }

        SetBusy(true);
        SetError(null);
        SetStatus("Opening file…");
        try
        {
            var inspection = await _fileService
                .InspectAsync(normalizedRoot, normalizedPath, cancellationToken)
                .ConfigureAwait(false);
            if (!inspection.CanOpenAsNormalText)
            {
                throw new ProjectEditorOpenException(
                    inspection.ContentKind == ProjectFileContentKind.Binary
                        ? "Binary files are not opened in the text editor."
                        : "This file exceeds the normal text-editor materialization limit.");
            }

            var snapshot = await _fileService
                .ReadTextAsync(normalizedRoot, normalizedPath, cancellationToken)
                .ConfigureAwait(false);
            var document = new ProjectEditorDocument(snapshot);
            document.PropertyChanged += OnDocumentPropertyChanged;
            _documents.Add(document);
            SelectedDocument = document;
            OnPropertyChanged(nameof(HasDocuments));
            SetStatus($"Opened {document.RelativePath}.");
            return document;
        }
        catch (Exception exception) when (exception is ProjectEditorOpenException
                                           or FileNotFoundException
                                           or DirectoryNotFoundException
                                           or UnauthorizedAccessException
                                           or IOException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            SetError(exception.Message);
            SetStatus("File open failed; no source file was modified.");
            throw;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public Task SaveSelectedAsync(CancellationToken cancellationToken = default) =>
        SaveAsync(
            SelectedDocument ?? throw new InvalidOperationException("Select an editor tab before saving."),
            cancellationToken);

    public async Task SaveAsync(
        ProjectEditorDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureOwnedDocument(document);
        if (!document.IsDirty)
        {
            SetError(null);
            SetStatus($"{document.RelativePath} has no unsaved changes.");
            return;
        }

        SetBusy(true);
        SetError(null);
        SetStatus($"Saving {document.RelativePath}…");
        try
        {
            var persistedText = ProjectEditorTextPolicy.NormalizeForSave(document.Text, document.NewLineStyle);
            var result = await _fileService.WriteTextAsync(
                    new ProjectTextFileWriteRequest(
                        document.ProjectRootPath,
                        document.FullPath,
                        persistedText,
                        document.Encoding,
                        document.Version),
                    cancellationToken)
                .ConfigureAwait(false);
            document.ApplySaved(result);
            SetStatus($"Saved {document.RelativePath}.");
        }
        catch (ProjectFileConflictException exception)
        {
            document.MarkConflict(exception.Message);
            SetError(exception.Message);
            SetStatus("Save blocked because the file changed on disk. Reload or reconcile the external change first.");
            throw;
        }
        catch (Exception exception) when (exception is FileNotFoundException
                                           or DirectoryNotFoundException
                                           or UnauthorizedAccessException
                                           or IOException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            SetError(exception.Message);
            SetStatus("Save failed; the editor kept the unsaved buffer.");
            throw;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public Task ReloadSelectedAsync(
        bool discardUnsavedChanges,
        CancellationToken cancellationToken = default) =>
        ReloadAsync(
            SelectedDocument ?? throw new InvalidOperationException("Select an editor tab before reloading."),
            discardUnsavedChanges,
            cancellationToken);

    public async Task ReloadAsync(
        ProjectEditorDocument document,
        bool discardUnsavedChanges,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureOwnedDocument(document);
        if (document.IsDirty && !discardUnsavedChanges)
        {
            throw new ProjectEditorDirtyException(
                $"{document.RelativePath} has unsaved changes. Explicitly discard them before reloading from disk.");
        }

        SetBusy(true);
        SetError(null);
        SetStatus($"Reloading {document.RelativePath}…");
        try
        {
            var inspection = await _fileService
                .InspectAsync(document.ProjectRootPath, document.FullPath, cancellationToken)
                .ConfigureAwait(false);
            if (!inspection.CanOpenAsNormalText)
            {
                throw new ProjectEditorOpenException(
                    "The file can no longer be materialized safely as normal text.");
            }

            var snapshot = await _fileService
                .ReadTextAsync(document.ProjectRootPath, document.FullPath, cancellationToken)
                .ConfigureAwait(false);
            document.ApplyReload(snapshot);
            SetStatus($"Reloaded {document.RelativePath} from disk.");
        }
        catch (Exception exception) when (exception is ProjectEditorOpenException
                                           or FileNotFoundException
                                           or DirectoryNotFoundException
                                           or UnauthorizedAccessException
                                           or IOException
                                           or ArgumentException
                                           or InvalidOperationException)
        {
            SetError(exception.Message);
            SetStatus("Reload failed; the existing editor buffer was retained.");
            throw;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public void CloseSelected(bool discardUnsavedChanges) =>
        Close(
            SelectedDocument ?? throw new InvalidOperationException("Select an editor tab before closing."),
            discardUnsavedChanges);

    public void Close(ProjectEditorDocument document, bool discardUnsavedChanges)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureOwnedDocument(document);
        if (document.IsDirty && !discardUnsavedChanges)
        {
            throw new ProjectEditorDirtyException(
                $"{document.RelativePath} has unsaved changes. Save or explicitly discard them before closing the tab.");
        }

        var index = _documents.IndexOf(document);
        document.PropertyChanged -= OnDocumentPropertyChanged;
        _documents.RemoveAt(index);
        if (ReferenceEquals(SelectedDocument, document))
        {
            SelectedDocument = _documents.Count == 0
                ? null
                : _documents[Math.Min(index, _documents.Count - 1)];
        }

        OnPropertyChanged(nameof(HasDocuments));
        SetError(null);
        SetStatus($"Closed {document.RelativePath}.");
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, SelectedDocument)
            && e.PropertyName is nameof(ProjectEditorDocument.IsDirty)
                or nameof(ProjectEditorDocument.HasConflict)
                or null)
        {
            RaiseCommandStateChanged();
        }
    }

    private void EnsureOwnedDocument(ProjectEditorDocument document)
    {
        if (!_documents.Contains(document))
        {
            throw new InvalidOperationException("The editor document does not belong to this workspace.");
        }
    }

    private void SetBusy(bool value)
    {
        if (_isBusy == value)
        {
            return;
        }

        _isBusy = value;
        OnPropertyChanged(nameof(IsBusy));
        RaiseCommandStateChanged();
    }

    private void SetStatus(string value)
    {
        if (string.Equals(_statusText, value, StringComparison.Ordinal))
        {
            return;
        }

        _statusText = value;
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetError(string? value)
    {
        if (string.Equals(_errorMessage, value, StringComparison.Ordinal))
        {
            return;
        }

        _errorMessage = value;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private void RaiseCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanReload));
        OnPropertyChanged(nameof(CanClose));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ProjectEditorDocument : INotifyPropertyChanged
{
    private string _text;
    private string _savedEditorText;
    private ProjectFileVersion _version;
    private bool _isDirty;
    private bool _hasConflict;
    private string? _conflictMessage;

    internal ProjectEditorDocument(ProjectTextFileSnapshot snapshot)
    {
        ApplySnapshot(snapshot, initialize: true);
        _text = snapshot.Text;
        _savedEditorText = snapshot.Text;
        _version = snapshot.Version;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProjectRootPath { get; private set; } = string.Empty;
    public string FullPath { get; private set; } = string.Empty;
    public string RelativePath { get; private set; } = string.Empty;
    public string FileName => Path.GetFileName(FullPath);
    public string LanguageLabel => ProjectEditorLanguageDetector.Detect(RelativePath);
    public ProjectTextEncoding Encoding { get; private set; }
    public ProjectNewLineStyle NewLineStyle { get; private set; }
    public bool EndsWithNewLine { get; private set; }
    public ProjectFileVersion Version => _version;
    public bool IsDirty => _isDirty;
    public bool HasConflict => _hasConflict;
    public string? ConflictMessage => _conflictMessage;
    public string DisplayLabel => IsDirty ? $"{FileName} *" : FileName;

    public string Text
    {
        get => _text;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_text, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _text = normalized;
            OnPropertyChanged();
            SetDirty(!string.Equals(_text, _savedEditorText, StringComparison.Ordinal));
        }
    }

    internal void ApplySaved(ProjectFileWriteResult result)
    {
        _version = result.Version;
        _savedEditorText = Text;
        _hasConflict = false;
        _conflictMessage = null;
        SetDirty(false);
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(HasConflict));
        OnPropertyChanged(nameof(ConflictMessage));
    }

    internal void ApplyReload(ProjectTextFileSnapshot snapshot) => ApplySnapshot(snapshot, initialize: false);

    internal void MarkConflict(string message)
    {
        _hasConflict = true;
        _conflictMessage = message;
        OnPropertyChanged(nameof(HasConflict));
        OnPropertyChanged(nameof(ConflictMessage));
    }

    private void ApplySnapshot(ProjectTextFileSnapshot snapshot, bool initialize)
    {
        ProjectRootPath = snapshot.ProjectRootPath;
        FullPath = snapshot.FullPath;
        RelativePath = snapshot.RelativePath;
        Encoding = snapshot.Encoding;
        NewLineStyle = snapshot.NewLineStyle;
        EndsWithNewLine = snapshot.EndsWithNewLine;
        _version = snapshot.Version;
        _text = snapshot.Text;
        _savedEditorText = snapshot.Text;
        _isDirty = false;
        _hasConflict = false;
        _conflictMessage = null;

        if (!initialize)
        {
            OnPropertyChanged(nameof(ProjectRootPath));
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(RelativePath));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(LanguageLabel));
            OnPropertyChanged(nameof(Encoding));
            OnPropertyChanged(nameof(NewLineStyle));
            OnPropertyChanged(nameof(EndsWithNewLine));
            OnPropertyChanged(nameof(Version));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(HasConflict));
            OnPropertyChanged(nameof(ConflictMessage));
            OnPropertyChanged(nameof(DisplayLabel));
        }
    }

    private void SetDirty(bool value)
    {
        if (_isDirty == value)
        {
            return;
        }

        _isDirty = value;
        if (_isDirty)
        {
            _hasConflict = false;
            _conflictMessage = null;
        }
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(HasConflict));
        OnPropertyChanged(nameof(ConflictMessage));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public static class ProjectEditorTextPolicy
{
    public static string NormalizeForSave(string text, ProjectNewLineStyle originalStyle)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (originalStyle == ProjectNewLineStyle.Mixed)
        {
            return text;
        }

        var separator = originalStyle switch
        {
            ProjectNewLineStyle.Lf => "\n",
            ProjectNewLineStyle.Cr => "\r",
            ProjectNewLineStyle.CrLf => "\r\n",
            ProjectNewLineStyle.None => "\r\n",
            _ => throw new ArgumentOutOfRangeException(nameof(originalStyle)),
        };

        var canonical = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return separator == "\n"
            ? canonical
            : canonical.Replace("\n", separator, StringComparison.Ordinal);
    }
}

public static class ProjectEditorLanguageDetector
{
    public static string Detect(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "C#",
            ".csproj" or ".props" or ".targets" => "MSBuild",
            ".xaml" => "XAML",
            ".json" => "JSON",
            ".md" => "Markdown",
            ".ps1" or ".psm1" => "PowerShell",
            ".js" or ".mjs" or ".cjs" => "JavaScript",
            ".ts" or ".tsx" => "TypeScript",
            ".html" or ".htm" => "HTML",
            ".css" => "CSS",
            ".xml" => "XML",
            ".yml" or ".yaml" => "YAML",
            ".py" => "Python",
            ".php" => "PHP",
            ".java" => "Java",
            ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp" => "C/C++",
            ".rs" => "Rust",
            ".go" => "Go",
            ".sh" => "Shell",
            _ => "Plain text",
        };
    }
}

public sealed class ProjectEditorOpenException : IOException
{
    public ProjectEditorOpenException(string message)
        : base(message)
    {
    }
}

public sealed class ProjectEditorDirtyException : InvalidOperationException
{
    public ProjectEditorDirtyException(string message)
        : base(message)
    {
    }
}
