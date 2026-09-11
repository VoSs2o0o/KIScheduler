using KIScheduler.Core.Contracts;
using KIScheduler.Infrastructure.Projects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Projects;

[TestClass]
public sealed class ProjectRootResolverTests
{
    private string? testDirectory;

    [TestInitialize]
    public void Initialize()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), $"kischeduler-project-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (testDirectory is not null && Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void PromptDirectlyInDocsResolvesItsParent()
    {
        string prompt = CreatePrompt("Repo", "docs", "000_AP0.md");

        ProjectRootResolution result = new ProjectRootResolver().Resolve(prompt);

        Assert.IsTrue(result.IsResolved);
        Assert.AreEqual(Path.Combine(testDirectory!, "Repo"), result.ProjectRoot);
    }

    [TestMethod]
    public void NestedPromptResolvesNearestMatchingPromptDirectory()
    {
        string prompt = CreatePrompt("outer", "docs", "Repo", "docprompts", "feature", "010_AP10.md");

        ProjectRootResolution result = new ProjectRootResolver().Resolve(prompt);

        Assert.AreEqual(ProjectRootResolutionStatus.Resolved, result.Status);
        Assert.AreEqual(Path.Combine(testDirectory!, "outer", "docs", "Repo"), result.ProjectRoot);
    }

    [TestMethod]
    public void PromptDirectoryComparisonIgnoresCase()
    {
        string prompt = CreatePrompt("Repo", "DoCs", "feature", "task.md");

        ProjectRootResolution result = new ProjectRootResolver().Resolve(prompt);

        Assert.AreEqual(Path.Combine(testDirectory!, "Repo"), result.ProjectRoot);
    }

    [TestMethod]
    public void RelativePromptPathIsNormalized()
    {
        string prompt = CreatePrompt("Repo", "docs", "task.md");
        string relativePrompt = Path.GetRelativePath(Environment.CurrentDirectory, prompt);

        ProjectRootResolution result = new ProjectRootResolver().Resolve(relativePrompt);

        Assert.AreEqual(Path.Combine(testDirectory!, "Repo"), result.ProjectRoot);
        Assert.AreEqual(Path.GetFullPath(prompt), result.PromptPath);
    }

    [TestMethod]
    public void PromptOutsideConventionReturnsProjectMissingWithoutGuessing()
    {
        string prompt = CreatePrompt("Repo", "other", "task.md");

        ProjectRootResolution result = new ProjectRootResolver().Resolve(prompt);

        Assert.AreEqual(ProjectRootResolutionStatus.ProjektFehlt, result.Status);
        Assert.IsNull(result.ProjectRoot);
        StringAssert.Contains(result.Message, "docs");
    }

    [TestMethod]
    public void MissingPromptReturnsUnderstandableError()
    {
        string prompt = Path.Combine(testDirectory!, "Repo", "docs", "missing.md");

        ProjectRootResolution result = new ProjectRootResolver().Resolve(prompt);

        Assert.AreEqual(ProjectRootResolutionStatus.PromptNotFound, result.Status);
        Assert.IsNull(result.ProjectRoot);
        StringAssert.Contains(result.Message, "nicht gefunden");
    }

    private string CreatePrompt(params string[] pathParts)
    {
        string path = pathParts.Aggregate(testDirectory!, Path.Combine);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "prompt");
        return path;
    }
}
