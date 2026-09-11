namespace KIScheduler.Core.Contracts;

public enum GitInspectionStatus
{
    Ready,
    GitUnavailable,
    ProjectDirectoryMissing,
    NotARepository,
    RepositoryRootMismatch,
    DetachedHead,
    BranchMismatch,
    WorkingTreeDirty,
    Failed
}

public sealed record GitInspectionRequest(
    string ProjectRoot,
    string TargetBranch,
    bool RequireCleanWorkingTree);

public sealed record GitWorkingTreeSnapshot(
    string RepositoryRoot,
    string? CurrentBranch,
    IReadOnlyList<string> StatusLines)
{
    public bool IsClean => StatusLines.Count == 0;
}

public sealed record GitInspectionResult(
    GitInspectionStatus Status,
    string Message,
    GitWorkingTreeSnapshot? Snapshot = null)
{
    public bool IsReady => Status == GitInspectionStatus.Ready;
}

public enum GitCommitStatus
{
    Committed,
    NoChanges,
    Failed
}

public sealed record GitCommitRequest(string ProjectRoot, string TargetBranch, string CommitMessage);

public sealed record GitCommitResult(
    GitCommitStatus Status,
    string Message,
    GitWorkingTreeSnapshot Before,
    GitWorkingTreeSnapshot? After = null,
    string? CommitId = null)
{
    public bool Succeeded => Status == GitCommitStatus.Committed;
}

public interface IGitService
{
    Task<GitInspectionResult> InspectAsync(
        GitInspectionRequest request,
        CancellationToken cancellationToken = default);

    Task<GitCommitResult> CommitAllAsync(
        GitCommitRequest request,
        CancellationToken cancellationToken = default);
}
