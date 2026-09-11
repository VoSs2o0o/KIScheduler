using KIScheduler.Core.Contracts;

namespace KIScheduler.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);
    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, content, cancellationToken);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);
}
