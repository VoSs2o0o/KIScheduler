using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Infrastructure.Projects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Projects;

[TestClass]
public sealed class ProjectCreationServiceTests
{
    private string? parentDirectory;

    [TestInitialize]
    public void Initialize()
    {
        parentDirectory = Path.Combine(Path.GetTempPath(), $"kischeduler-create-{Guid.NewGuid():N}");
        Directory.CreateDirectory(parentDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (parentDirectory is not null && Directory.Exists(parentDirectory))
        {
            Directory.Delete(parentDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task UnconfirmedSelectionNeverStartsProcessOrPersistsProject()
    {
        var runner = new RecordingProcessRunner(Success());
        var repository = new RecordingProjectRepository();
        ProjectDefinition project = CreateProject();

        ProjectCreationResult result = await new ProjectCreationService(runner, repository)
            .CreateAsync(new ProjectCreationRequest(project, IsExplicitlyConfirmed: false));

        Assert.AreEqual(ProjectCreationStatus.ConfirmationRequired, result.Status);
        Assert.IsNull(runner.Request);
        Assert.IsNull(repository.SavedProject);
    }

    [TestMethod]
    public async Task ConfirmedSelectionUsesSafeDotnetArgumentListAndPersistsDefinition()
    {
        var runner = new RecordingProcessRunner(Success());
        var repository = new RecordingProjectRepository();
        ProjectDefinition project = CreateProject();

        ProjectCreationResult result = await new ProjectCreationService(runner, repository)
            .CreateAsync(new ProjectCreationRequest(project, IsExplicitlyConfirmed: true));

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(runner.Request);
        Assert.AreEqual("dotnet", runner.Request.FileName);
        CollectionAssert.AreEqual(new[]
        {
            "new", "console", "--output", project.RootPath, "--name", project.Name
        }, runner.Request.Arguments.ToArray());
        Assert.AreSame(project, repository.SavedProject);
        ProjectDefinition savedProject = repository.SavedProject!;
        Assert.AreEqual("develop", savedProject.TargetBranch);
        Assert.AreEqual("console", savedProject.DefaultTemplate);
        Assert.AreEqual(1, savedProject.ValidationCommands.Count);
    }

    [TestMethod]
    public async Task ExistingRootDoesNotStartProjectCreation()
    {
        var runner = new RecordingProcessRunner(Success());
        var repository = new RecordingProjectRepository();
        ProjectDefinition project = CreateProject();
        Directory.CreateDirectory(project.RootPath);

        ProjectCreationResult result = await new ProjectCreationService(runner, repository)
            .CreateAsync(new ProjectCreationRequest(project, IsExplicitlyConfirmed: true));

        Assert.AreEqual(ProjectCreationStatus.ProjectRootAlreadyExists, result.Status);
        Assert.IsNull(runner.Request);
    }

    [TestMethod]
    public async Task FailedDotnetNewIsReportedAndNotPersisted()
    {
        var processResult = new ProcessRunResult(ProcessTerminationReason.Completed, 1, TimeSpan.Zero,
        [
            new ProcessOutputLine(1, ProcessOutputStream.StandardError, "Vorlage unbekannt", DateTimeOffset.UtcNow)
        ]);
        var runner = new RecordingProcessRunner(processResult);
        var repository = new RecordingProjectRepository();

        ProjectCreationResult result = await new ProjectCreationService(runner, repository)
            .CreateAsync(new ProjectCreationRequest(CreateProject(), IsExplicitlyConfirmed: true));

        Assert.AreEqual(ProjectCreationStatus.ProcessFailed, result.Status);
        StringAssert.Contains(result.Message, "Vorlage unbekannt");
        Assert.IsNull(repository.SavedProject);
    }

    private ProjectDefinition CreateProject() => new(ProjectId.New(), "My App",
        Path.Combine(parentDirectory!, "My App"), "develop",
        [new ValidationCommand("dotnet", ["test"])], "console");

    private static ProcessRunResult Success() => new(
        ProcessTerminationReason.Completed, 0, TimeSpan.Zero, Array.Empty<ProcessOutputLine>());

    private sealed class RecordingProcessRunner(ProcessRunResult result) : IProcessRunner
    {
        public ProcessRunRequest? Request { get; private set; }

        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingProjectRepository : IProjectRepository
    {
        public ProjectDefinition? SavedProject { get; private set; }
        public Task<ProjectDefinition?> GetAsync(ProjectId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectDefinition?>(null);
        public Task<IReadOnlyList<ProjectDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectDefinition>>(Array.Empty<ProjectDefinition>());
        public Task SaveAsync(ProjectDefinition project, CancellationToken cancellationToken = default)
        {
            SavedProject = project;
            return Task.CompletedTask;
        }
        public Task<bool> DeleteAsync(ProjectId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
