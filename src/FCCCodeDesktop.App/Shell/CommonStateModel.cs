using System.Windows.Input;

namespace FCCCodeDesktop.App.Shell;

public enum CommonStateKind
{
    Empty,
    Loading,
    Info,
    Success,
    Warning,
    Error,
    Unavailable,
    Offline,
    Blocked,
}

public sealed class CommonStateModel
{
    public CommonStateModel(
        CommonStateKind kind,
        string title,
        string message,
        string? detail = null,
        string? actionLabel = null,
        ICommand? actionCommand = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown common state kind.");
        }

        Kind = kind;
        Title = RequireText(title, nameof(title));
        Message = RequireText(message, nameof(message));
        Detail = NormalizeOptionalText(detail);
        ActionLabel = NormalizeOptionalText(actionLabel);
        ActionCommand = actionCommand;

        if ((ActionLabel is null) != (ActionCommand is null))
        {
            throw new ArgumentException("Action label and action command must be supplied together.");
        }
    }

    public CommonStateKind Kind { get; }

    public string Title { get; }

    public string Message { get; }

    public string? Detail { get; }

    public string? ActionLabel { get; }

    public ICommand? ActionCommand { get; }

    public bool IsBusy => Kind == CommonStateKind.Loading;

    public bool HasDetail => Detail is not null;

    public bool HasAction => ActionLabel is not null && ActionCommand is not null;

    public static CommonStateModel Empty(string title, string message, string? detail = null) =>
        Create(title, message, detail, null, null, CommonStateKind.Empty);

    public static CommonStateModel Loading(string title, string message, string? detail = null) =>
        Create(title, message, detail, null, null, CommonStateKind.Loading);

    public static CommonStateModel Info(string title, string message, string? detail = null) =>
        Create(title, message, detail, null, null, CommonStateKind.Info);

    public static CommonStateModel Success(string title, string message, string? detail = null) =>
        Create(title, message, detail, null, null, CommonStateKind.Success);

    public static CommonStateModel Warning(string title, string message, string? detail = null) =>
        Create(title, message, detail, null, null, CommonStateKind.Warning);

    public static CommonStateModel Error(
        string title,
        string message,
        string? detail = null,
        string? actionLabel = null,
        ICommand? actionCommand = null) =>
        Create(title, message, detail, actionLabel, actionCommand, CommonStateKind.Error);

    public static CommonStateModel Unavailable(
        string title,
        string message,
        string? detail = null,
        string? actionLabel = null,
        ICommand? actionCommand = null) =>
        Create(title, message, detail, actionLabel, actionCommand, CommonStateKind.Unavailable);

    public static CommonStateModel Offline(
        string title,
        string message,
        string? detail = null,
        string? actionLabel = null,
        ICommand? actionCommand = null) =>
        Create(title, message, detail, actionLabel, actionCommand, CommonStateKind.Offline);

    public static CommonStateModel Blocked(
        string title,
        string message,
        string? detail = null,
        string? actionLabel = null,
        ICommand? actionCommand = null) =>
        Create(title, message, detail, actionLabel, actionCommand, CommonStateKind.Blocked);

    private static CommonStateModel Create(
        string title,
        string message,
        string? detail,
        string? actionLabel,
        ICommand? actionCommand,
        CommonStateKind kind) =>
        new(kind, title, message, detail, actionLabel, actionCommand);

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
