using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Contracts;

public enum ProjectRootResolutionStatus
{
    Resolved,
    ProjektFehlt,
    InvalidPath,
    PromptNotFound,
    AccessDenied
}

public sealed record ProjectRootResolution(
    ProjectRootResolutionStatus Status,
    string? PromptPath,
    string? ProjectRoot,
    string Message)
{
    public bool IsResolved => Status == ProjectRootResolutionStatus.Resolved;
}

public interface IProjectRootResolver
{
    ProjectRootResolution Resolve(string promptPath);
}

/// <summary>
/// Data needed by the later UI when automatic project-root resolution is not possible.
/// The selected path must be presented to the user and explicitly confirmed before it is used.
/// </summary>
public sealed record ProjectRootSelectionRequest(
    string PromptPath,
    ProjectRootResolutionStatus Reason,
    string Message,
    string? InitialDirectory = null);

public sealed record ProjectRootSelection(string SelectedRootPath, bool IsExplicitlyConfirmed);

public sealed record ProjectCreationRequest(ProjectDefinition Project, bool IsExplicitlyConfirmed);

public enum ProjectCreationStatus
{
    Created,
    ConfirmationRequired,
    InvalidProjectRoot,
    ParentDirectoryNotFound,
    ProjectRootAlreadyExists,
    ProcessFailed
}

public sealed record ProjectCreationResult(
    ProjectCreationStatus Status,
    string Message,
    ProcessRunResult? ProcessResult = null)
{
    public bool Succeeded => Status == ProjectCreationStatus.Created;
}

public interface IProjectCreationService
{
    Task<ProjectCreationResult> CreateAsync(
        ProjectCreationRequest request,
        CancellationToken cancellationToken = default);
}
