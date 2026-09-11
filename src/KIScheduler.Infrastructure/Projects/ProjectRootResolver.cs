using KIScheduler.Core.Contracts;

namespace KIScheduler.Infrastructure.Projects;

public sealed class ProjectRootResolver : IProjectRootResolver
{
    private static readonly HashSet<string> PromptDirectoryNames =
        new(["docs", "docprompts"], StringComparer.OrdinalIgnoreCase);

    public ProjectRootResolution Resolve(string promptPath)
    {
        if (string.IsNullOrWhiteSpace(promptPath))
        {
            return Failure(ProjectRootResolutionStatus.InvalidPath, null,
                "Der Prompt-Pfad darf nicht leer sein.");
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(promptPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure(ProjectRootResolutionStatus.InvalidPath, null,
                $"Der Prompt-Pfad ist ungültig: {exception.Message}");
        }

        try
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(normalizedPath);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return Failure(ProjectRootResolutionStatus.PromptNotFound, normalizedPath,
                    $"Die Prompt-Datei wurde nicht gefunden: {normalizedPath}");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                return Failure(ProjectRootResolutionStatus.PromptNotFound, normalizedPath,
                    $"Der Prompt-Pfad bezeichnet keine Datei: {normalizedPath}");
            }

            var promptFile = new FileInfo(normalizedPath);
            var directory = promptFile.Directory;

            while (directory is not null)
            {
                if (PromptDirectoryNames.Contains(directory.Name))
                {
                    DirectoryInfo? parent = directory.Parent;
                    if (parent is null)
                    {
                        return Failure(ProjectRootResolutionStatus.ProjektFehlt, normalizedPath,
                            $"Der Ordner '{directory.Name}' besitzt kein übergeordnetes Projektverzeichnis.");
                    }

                    return new ProjectRootResolution(
                        ProjectRootResolutionStatus.Resolved,
                        normalizedPath,
                        Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent.FullName)),
                        "Das Projektroot wurde aus dem Prompt-Pfad ermittelt.");
                }

                directory = directory.Parent;
            }

            return Failure(ProjectRootResolutionStatus.ProjektFehlt, normalizedPath,
                "Im Prompt-Pfad wurde kein übergeordneter Ordner 'docs' oder 'docprompts' gefunden.");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ProjectRootResolutionStatus.AccessDenied, normalizedPath,
                $"Auf den Prompt-Pfad kann nicht zugegriffen werden: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or System.Security.SecurityException)
        {
            return Failure(ProjectRootResolutionStatus.InvalidPath, normalizedPath,
                $"Der Prompt-Pfad konnte nicht aufgelöst werden: {exception.Message}");
        }
    }

    private static ProjectRootResolution Failure(
        ProjectRootResolutionStatus status,
        string? promptPath,
        string message) => new(status, promptPath, null, message);
}
