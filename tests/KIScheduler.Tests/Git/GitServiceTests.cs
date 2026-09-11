using KIScheduler.Core.Contracts;
using KIScheduler.Infrastructure;
using KIScheduler.Infrastructure.Git;
using KIScheduler.Infrastructure.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Git;

[TestClass]
public sealed class GitServiceTests
{
    private string? repositoryRoot;
    private LocalProcessRunner? runner;
    private GitService? service;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        repositoryRoot = Path.Combine(Path.GetTempPath(), $"kischeduler-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repositoryRoot);
        runner = new LocalProcessRunner(NullLogger<LocalProcessRunner>.Instance);
        service = new GitService(runner, new PhysicalFileSystem(), NullLogger<GitService>.Instance);

        await GitAsync("init", "--initial-branch", "master");
        await GitAsync("config", "user.name", "KIScheduler Tests");
        await GitAsync("config", "user.email", "kischeduler-tests@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(repositoryRoot, "modified.txt"), "before");
        await File.WriteAllTextAsync(Path.Combine(repositoryRoot, "deleted.txt"), "remove me");
        await GitAsync("add", "-A");
        await GitAsync("commit", "--message", "initial");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (repositoryRoot is not null && Directory.Exists(repositoryRoot))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(repositoryRoot, "*", SearchOption.AllDirectories))
                File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task WrongBranchFailsPreflight()
    {
        GitInspectionResult result = await service!.InspectAsync(
            new GitInspectionRequest(repositoryRoot!, "develop", RequireCleanWorkingTree: true));

        Assert.AreEqual(GitInspectionStatus.BranchMismatch, result.Status);
        Assert.AreEqual("master", result.Snapshot?.CurrentBranch);
    }

    [TestMethod]
    public async Task DirtyWorkingTreeFailsAutoCommitPreflightWithoutStagingChanges()
    {
        await File.AppendAllTextAsync(Path.Combine(repositoryRoot!, "modified.txt"), " after");

        GitInspectionResult result = await service!.InspectAsync(
            new GitInspectionRequest(repositoryRoot!, "master", RequireCleanWorkingTree: true));

        Assert.AreEqual(GitInspectionStatus.WorkingTreeDirty, result.Status);
        Assert.IsFalse(result.Snapshot!.IsClean);
        ProcessRunResult staged = await GitAsync("diff", "--cached", "--name-only");
        Assert.AreEqual(0, staged.StandardOutput.Count);
    }

    [TestMethod]
    public async Task CommitAllIncludesCreatedModifiedAndDeletedFilesInOneCommit()
    {
        await File.AppendAllTextAsync(Path.Combine(repositoryRoot!, "modified.txt"), " after");
        File.Delete(Path.Combine(repositoryRoot!, "deleted.txt"));
        await File.WriteAllTextAsync(Path.Combine(repositoryRoot!, "created.txt"), "new");

        GitCommitResult result = await service!.CommitAllAsync(
            new GitCommitRequest(repositoryRoot!, "master", "AP9: integration test"));

        Assert.AreEqual(GitCommitStatus.Committed, result.Status);
        Assert.IsNotNull(result.CommitId);
        Assert.IsTrue(result.After!.IsClean);
        ProcessRunResult names = await GitAsync("show", "--pretty=format:", "--name-status", "HEAD");
        string[] changes = names.StandardOutput.ToArray();
        CollectionAssert.Contains(changes, "A\tcreated.txt");
        CollectionAssert.Contains(changes, "D\tdeleted.txt");
        CollectionAssert.Contains(changes, "M\tmodified.txt");
        ProcessRunResult count = await GitAsync("rev-list", "--count", "HEAD");
        Assert.AreEqual("2", count.StandardOutput.Single());
    }

    [TestMethod]
    public async Task CleanTreeCreatesNoEmptyCommit()
    {
        ProcessRunResult before = await GitAsync("rev-parse", "HEAD");

        GitCommitResult result = await service!.CommitAllAsync(
            new GitCommitRequest(repositoryRoot!, "master", "must not exist"));

        ProcessRunResult after = await GitAsync("rev-parse", "HEAD");
        Assert.AreEqual(GitCommitStatus.NoChanges, result.Status);
        Assert.AreEqual(before.StandardOutput.Single(), after.StandardOutput.Single());
    }

    [TestMethod]
    public async Task ConfiguredSubdirectoryIsRejectedAsRepositoryRoot()
    {
        string subdirectory = Path.Combine(repositoryRoot!, "src");
        Directory.CreateDirectory(subdirectory);

        GitInspectionResult result = await service!.InspectAsync(
            new GitInspectionRequest(subdirectory, "master", RequireCleanWorkingTree: true));

        Assert.AreEqual(GitInspectionStatus.RepositoryRootMismatch, result.Status);
    }

    private async Task<ProcessRunResult> GitAsync(params string[] arguments)
    {
        ProcessRunResult result = await runner!.RunAsync(new ProcessRunRequest("git")
        {
            Arguments = arguments,
            WorkingDirectory = repositoryRoot,
            Timeout = TimeSpan.FromSeconds(15)
        });
        Assert.IsTrue(result.Succeeded,
            $"git {string.Join(' ', arguments)} failed: {string.Join(Environment.NewLine, result.StandardError)}");
        return result;
    }
}
