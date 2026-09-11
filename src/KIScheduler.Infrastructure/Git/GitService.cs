using KIScheduler.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace KIScheduler.Infrastructure.Git;

public sealed class GitService(
    IProcessRunner processRunner,
    IFileSystem fileSystem,
    ILogger<GitService> logger) : IGitService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    public async Task<GitInspectionResult> InspectAsync(
        GitInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetBranch);

        string configuredRoot;
        try
        {
            configuredRoot = NormalizePath(request.ProjectRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failed(GitInspectionStatus.ProjectDirectoryMissing,
                $"Der konfigurierte Projektpfad ist ungültig: {exception.Message}");
        }

        if (!fileSystem.DirectoryExists(configuredRoot))
            return Failed(GitInspectionStatus.ProjectDirectoryMissing,
                $"Das Projektverzeichnis '{configuredRoot}' existiert nicht.");

        ProcessRunResult version = await RunGitAsync(null, ["--version"], cancellationToken).ConfigureAwait(false);
        if (!version.Succeeded)
            return Failed(GitInspectionStatus.GitUnavailable,
                $"Git ist nicht verfügbar: {DescribeFailure(version)}");

        ProcessRunResult rootResult = await RunGitAsync(configuredRoot,
            ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false);
        if (!rootResult.Succeeded || LastOutput(rootResult) is not { } repositoryRootText)
            return Failed(GitInspectionStatus.NotARepository,
                $"Das Projektverzeichnis ist kein lesbares Git-Repository: {DescribeFailure(rootResult)}");

        string repositoryRoot;
        try
        {
            repositoryRoot = NormalizePath(repositoryRootText);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failed(GitInspectionStatus.Failed,
                $"Git meldete ein ungültiges Repositoryroot: {exception.Message}");
        }

        if (!PathEquals(repositoryRoot, configuredRoot))
        {
            var mismatchSnapshot = new GitWorkingTreeSnapshot(repositoryRoot, null, Array.Empty<string>());
            return Failed(GitInspectionStatus.RepositoryRootMismatch,
                $"Das konfigurierte Projektroot '{configuredRoot}' entspricht nicht dem Git-Repositoryroot '{repositoryRoot}'.",
                mismatchSnapshot);
        }

        ProcessRunResult branchResult = await RunGitAsync(repositoryRoot,
            ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken).ConfigureAwait(false);
        if (!branchResult.Succeeded || LastOutput(branchResult) is not { } branch)
        {
            var detachedSnapshot = new GitWorkingTreeSnapshot(repositoryRoot, null, Array.Empty<string>());
            return Failed(GitInspectionStatus.DetachedHead,
                "Das Repository befindet sich nicht auf einem benannten Branch.", detachedSnapshot);
        }

        ProcessRunResult statusResult = await RunGitAsync(repositoryRoot,
            ["status", "--porcelain=v1", "--untracked-files=all"], cancellationToken).ConfigureAwait(false);
        if (!statusResult.Succeeded)
        {
            var failedSnapshot = new GitWorkingTreeSnapshot(repositoryRoot, branch, Array.Empty<string>());
            return Failed(GitInspectionStatus.Failed,
                $"Der Git-Arbeitsbaumstatus konnte nicht gelesen werden: {DescribeFailure(statusResult)}", failedSnapshot);
        }

        var snapshot = new GitWorkingTreeSnapshot(repositoryRoot, branch, statusResult.StandardOutput.ToArray());
        logger.LogInformation(
            "Git status for {RepositoryRoot}: branch {Branch}, clean {IsClean}, status {Status}",
            repositoryRoot, branch, snapshot.IsClean, FormatStatus(snapshot));

        if (!string.Equals(branch, request.TargetBranch.Trim(), StringComparison.Ordinal))
            return Failed(GitInspectionStatus.BranchMismatch,
                $"Aktueller Branch '{branch}' entspricht nicht dem Zielbranch '{request.TargetBranch.Trim()}'.", snapshot);

        if (request.RequireCleanWorkingTree && !snapshot.IsClean)
            return Failed(GitInspectionStatus.WorkingTreeDirty,
                "Der Arbeitsbaum ist vor dem Auto-Commit-Auftrag nicht sauber.", snapshot);

        return new GitInspectionResult(GitInspectionStatus.Ready,
            snapshot.IsClean ? "Git-Prüfung erfolgreich; Arbeitsbaum ist sauber."
                : "Git-Prüfung erfolgreich; vorhandene Änderungen wurden nur protokolliert.", snapshot);
    }

    public async Task<GitCommitResult> CommitAllAsync(
        GitCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CommitMessage);

        GitInspectionResult inspection = await InspectAsync(
            new GitInspectionRequest(request.ProjectRoot, request.TargetBranch, RequireCleanWorkingTree: false),
            cancellationToken).ConfigureAwait(false);
        if (!inspection.IsReady || inspection.Snapshot is null)
        {
            var unavailableSnapshot = inspection.Snapshot
                ?? new GitWorkingTreeSnapshot(request.ProjectRoot, null, Array.Empty<string>());
            return new GitCommitResult(GitCommitStatus.Failed, inspection.Message, unavailableSnapshot);
        }

        GitWorkingTreeSnapshot before = inspection.Snapshot;
        logger.LogInformation("Git status before commit in {RepositoryRoot}: {Status}",
            before.RepositoryRoot, FormatStatus(before));
        if (before.IsClean)
            return new GitCommitResult(GitCommitStatus.NoChanges,
                "Der Auftrag hat keine Dateiänderungen erzeugt; es wurde kein Commit angelegt.", before, before);

        ProcessRunResult addResult = await RunGitAsync(before.RepositoryRoot, ["add", "-A"], cancellationToken)
            .ConfigureAwait(false);
        if (!addResult.Succeeded)
            return new GitCommitResult(GitCommitStatus.Failed,
                $"Git konnte die Auftragsänderungen nicht stagen: {DescribeFailure(addResult)}", before);

        ProcessRunResult stagedChanges = await RunGitAsync(before.RepositoryRoot,
            ["diff", "--cached", "--quiet", "--exit-code"], cancellationToken).ConfigureAwait(false);
        if (stagedChanges.TerminationReason != ProcessTerminationReason.Completed
            || stagedChanges.ExitCode is not (0 or 1))
            return new GitCommitResult(GitCommitStatus.Failed,
                $"Git konnte die gestagten Änderungen nicht prüfen: {DescribeFailure(stagedChanges)}", before);
        if (stagedChanges.ExitCode == 0)
            return new GitCommitResult(GitCommitStatus.NoChanges,
                "Nach dem Staging waren keine commitfähigen Änderungen vorhanden; es wurde kein Commit angelegt.",
                before, before);

        ProcessRunResult commitResult = await RunGitAsync(before.RepositoryRoot,
            ["commit", "--message", request.CommitMessage.Trim()], cancellationToken).ConfigureAwait(false);
        if (!commitResult.Succeeded)
            return new GitCommitResult(GitCommitStatus.Failed,
                $"Git konnte den Commit nicht erzeugen: {DescribeFailure(commitResult)}", before);

        string? commitId = null;
        ProcessRunResult idResult = await RunGitAsync(before.RepositoryRoot, ["rev-parse", "HEAD"], cancellationToken)
            .ConfigureAwait(false);
        if (idResult.Succeeded) commitId = LastOutput(idResult);

        GitInspectionResult afterInspection = await InspectAsync(
            new GitInspectionRequest(request.ProjectRoot, request.TargetBranch, RequireCleanWorkingTree: false),
            cancellationToken).ConfigureAwait(false);
        GitWorkingTreeSnapshot? after = afterInspection.Snapshot;
        logger.LogInformation("Git status after commit in {RepositoryRoot}: {Status}; commit {CommitId}",
            before.RepositoryRoot, after is null ? afterInspection.Message : FormatStatus(after), commitId ?? "<unknown>");
        if (!afterInspection.IsReady || after is null || !after.IsClean)
            return new GitCommitResult(GitCommitStatus.Failed,
                $"Der Commit wurde erzeugt, aber die anschließende Git-Prüfung war nicht sauber: {afterInspection.Message}",
                before, after, commitId);
        return new GitCommitResult(GitCommitStatus.Committed,
            "Die Auftragsänderungen wurden in genau einem Commit gespeichert.", before, after, commitId);
    }

    private Task<ProcessRunResult> RunGitAsync(
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        processRunner.RunAsync(new ProcessRunRequest("git")
        {
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            Timeout = CommandTimeout
        }, cancellationToken);

    private static GitInspectionResult Failed(
        GitInspectionStatus status,
        string message,
        GitWorkingTreeSnapshot? snapshot = null) => new(status, message, snapshot);

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    private static bool PathEquals(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string? LastOutput(ProcessRunResult result) => result.StandardOutput
        .LastOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim();

    private static string DescribeFailure(ProcessRunResult result)
    {
        string? diagnostic = result.StandardError.Concat(result.StandardOutput)
            .LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
        return diagnostic?.Trim() ?? result.StartError ??
            $"Prozessende {result.TerminationReason}, Exitcode {result.ExitCode?.ToString() ?? "unbekannt"}";
    }

    private static string FormatStatus(GitWorkingTreeSnapshot snapshot) => snapshot.IsClean
        ? "clean"
        : string.Join(" | ", snapshot.StatusLines);
}
