namespace FCCCodeDesktop.Application.Git;

public enum GitBranchMutationKind
{
    CreateAndCheckout = 0,
    Checkout = 1,
}

public enum GitBranchMutationStatus
{
    Success = 0,
    NotRepository = 1,
    BareRepository = 2,
    GitUnavailable = 3,
    InvalidBranchName = 4,
    BranchNotFound = 5,
    BranchAlreadyExists = 6,
    CheckoutBlocked = 7,
    QueryFailed = 8,
}

public sealed record GitBranchMutationResult(
    GitBranchMutationStatus Status,
    GitBranchMutationKind Kind,
    string RequestedBranchName,
    string? RepositoryRootPath,
    string? PreviousBranchName,
    string? CurrentBranchName,
    string? FailureMessage = null)
{
    public bool IsSuccess => Status == GitBranchMutationStatus.Success;
}

/// <summary>
/// Explicit local branch mutations. Implementations must not force checkout, discard work-tree
/// changes, contact remotes, or silently overwrite owner work.
/// </summary>
public interface IGitBranchService
{
    Task<GitBranchMutationResult> CreateAndCheckoutAsync(
        string path,
        string branchName,
        CancellationToken cancellationToken = default);

    Task<GitBranchMutationResult> CheckoutAsync(
        string path,
        string branchName,
        CancellationToken cancellationToken = default);
}
