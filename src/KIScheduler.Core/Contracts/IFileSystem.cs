namespace KIScheduler.Core.Contracts;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
}
