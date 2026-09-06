using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.App.Projects;

public sealed class ProjectFileTreeNode : INotifyPropertyChanged
{
    private readonly ObservableCollection<ProjectFileTreeNode> _children = [];
    private readonly ReadOnlyObservableCollection<ProjectFileTreeNode> _readonlyChildren;
    private bool _childrenLoaded;
    private bool _isLoading;

    private ProjectFileTreeNode(
        string displayName,
        string fullPath,
        string relativePath,
        bool isDirectory,
        bool isReparsePoint,
        bool isPlaceholder,
        bool isError)
    {
        DisplayName = displayName;
        FullPath = fullPath;
        RelativePath = relativePath;
        IsDirectory = isDirectory;
        IsReparsePoint = isReparsePoint;
        IsPlaceholder = isPlaceholder;
        IsError = isError;
        _readonlyChildren = new ReadOnlyObservableCollection<ProjectFileTreeNode>(_children);

        if (CanExpand)
        {
            _children.Add(CreatePlaceholder("Expand to load…"));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; }

    public string FullPath { get; }

    public string RelativePath { get; }

    public bool IsDirectory { get; }

    public bool IsReparsePoint { get; }

    public bool IsPlaceholder { get; }

    public bool IsError { get; }

    public bool CanExpand => IsDirectory && !IsReparsePoint && !IsPlaceholder;

    public bool ChildrenLoaded => _childrenLoaded;

    public bool IsLoading => _isLoading;

    public ReadOnlyObservableCollection<ProjectFileTreeNode> Children => _readonlyChildren;

    public static ProjectFileTreeNode CreateRoot(string rootPath, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new ProjectFileTreeNode(
            displayName,
            Path.GetFullPath(rootPath),
            ".",
            isDirectory: true,
            isReparsePoint: false,
            isPlaceholder: false,
            isError: false);
    }

    public static ProjectFileTreeNode CreateEntry(ProjectFileSystemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new ProjectFileTreeNode(
            entry.Name,
            entry.FullPath,
            entry.RelativePath,
            entry.IsDirectory,
            entry.IsReparsePoint,
            isPlaceholder: false,
            isError: false);
    }

    internal void BeginLoading()
    {
        if (!CanExpand || ChildrenLoaded || IsLoading)
        {
            return;
        }

        _isLoading = true;
        _children.Clear();
        _children.Add(CreatePlaceholder("Loading directory…"));
        OnPropertyChanged(nameof(IsLoading));
    }

    internal void CompleteLoading(ProjectDirectoryListing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        _children.Clear();
        foreach (var entry in listing.Entries)
        {
            _children.Add(CreateEntry(entry));
        }

        if (listing.LimitReached)
        {
            _children.Add(
                CreatePlaceholder(
                    $"Showing the first {listing.MaximumEntries:N0} entries in this directory."));
        }
        else if (listing.Entries.Count == 0)
        {
            _children.Add(CreatePlaceholder("This directory is empty."));
        }

        _childrenLoaded = true;
        _isLoading = false;
        OnPropertyChanged(nameof(ChildrenLoaded));
        OnPropertyChanged(nameof(IsLoading));
    }

    internal void FailLoading(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _children.Clear();
        _children.Add(CreateError(message));
        _childrenLoaded = true;
        _isLoading = false;
        OnPropertyChanged(nameof(ChildrenLoaded));
        OnPropertyChanged(nameof(IsLoading));
    }

    private static ProjectFileTreeNode CreatePlaceholder(string message) =>
        new(
            message,
            string.Empty,
            string.Empty,
            isDirectory: false,
            isReparsePoint: false,
            isPlaceholder: true,
            isError: false);

    private static ProjectFileTreeNode CreateError(string message) =>
        new(
            message,
            string.Empty,
            string.Empty,
            isDirectory: false,
            isReparsePoint: false,
            isPlaceholder: true,
            isError: true);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
