using KIScheduler.Core.Contracts;

namespace KIScheduler.Infrastructure.Projects;

public sealed class ProjectCreationService(IProcessRunner processRunner, IProjectRepository projectRepository)
    : IProjectCreationService
{
    public async Task<ProjectCreationResult> CreateAsync(
        ProjectCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Project);

        if (!request.IsExplicitlyConfirmed)
        {
            return Result(ProjectCreationStatus.ConfirmationRequired,
                "Das Projekt wird erst nach einer ausdrücklichen Bestätigung erzeugt.");
        }

        string rootPath;
        try
        {
            rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.Project.RootPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result(ProjectCreationStatus.InvalidProjectRoot,
                $"Das gewählte Projektroot ist ungültig: {exception.Message}");
        }

        if (Directory.Exists(rootPath) || File.Exists(rootPath))
        {
            return Result(ProjectCreationStatus.ProjectRootAlreadyExists,
                $"Das gewählte Projektroot existiert bereits: {rootPath}");
        }

        string? parentPath = Path.GetDirectoryName(rootPath);
        if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
        {
            return Result(ProjectCreationStatus.ParentDirectoryNotFound,
                $"Das übergeordnete Verzeichnis existiert nicht: {parentPath ?? rootPath}");
        }

        var processRequest = new ProcessRunRequest("dotnet")
        {
            WorkingDirectory = parentPath,
            Arguments =
            [
                "new",
                request.Project.DefaultTemplate,
                "--output",
                rootPath,
                "--name",
                request.Project.Name
            ]
        };

        ProcessRunResult processResult = await processRunner.RunAsync(processRequest, cancellationToken)
            .ConfigureAwait(false);
        if (!processResult.Succeeded)
        {
            string detail = processResult.StartError
                ?? processResult.StandardError.LastOrDefault()
                ?? $"Exitcode {processResult.ExitCode?.ToString() ?? "unbekannt"}";
            return new ProjectCreationResult(ProjectCreationStatus.ProcessFailed,
                $"Das Projekt konnte nicht erzeugt werden: {detail}", processResult);
        }

        await projectRepository.SaveAsync(request.Project, cancellationToken).ConfigureAwait(false);
        return new ProjectCreationResult(ProjectCreationStatus.Created,
            $"Das Projekt wurde unter '{rootPath}' erzeugt und gespeichert.", processResult);
    }

    private static ProjectCreationResult Result(ProjectCreationStatus status, string message) => new(status, message);
}
