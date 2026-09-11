using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Platforms;
using KIScheduler.Platforms.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Platforms;

[TestClass]
public sealed class PlatformContractsTests
{
    private static readonly PlatformId CodexId = new("codex");
    private static readonly ModelId ModelId = new("gpt-test");
    private static readonly EffortLevel Medium = new("medium");

    [TestMethod]
    public void PlatformAndUsageProviderResolveIndependentlyBySameId()
    {
        var services = new ServiceCollection();
        services.AddKischedulerPlatformServices();
        services.AddSingleton<IAiPlatform>(new FakeAiPlatform(CodexId));
        services.AddSingleton<IUsageProvider>(new FakeUsageProvider(CodexId));

        using var provider = services.BuildServiceProvider();
        var platform = provider.GetRequiredService<IAiPlatformRegistry>().GetRequired(new PlatformId("CODEX"));
        var usage = provider.GetRequiredService<IUsageProviderRegistry>().GetRequired(CodexId);

        Assert.IsInstanceOfType(platform, typeof(FakeAiPlatform));
        Assert.IsInstanceOfType(usage, typeof(FakeUsageProvider));
        Assert.AreNotSame(platform, usage);
    }

    [TestMethod]
    public void DuplicateUsageProviderIsRejected()
    {
        IUsageProvider[] providers =
        [
            new FakeUsageProvider(CodexId),
            new FakeUsageProvider(new PlatformId("CODEX"))
        ];

        Assert.ThrowsException<PlatformRegistrationException>(() => new UsageProviderRegistry(providers));
    }

    [TestMethod]
    public void EveryEnabledPlatformNeedsExecutionAndUsageRegistration()
    {
        var platforms = new AiPlatformRegistry([new FakeAiPlatform(CodexId)]);
        var usage = new UsageProviderRegistry([]);
        var validator = new PlatformConfigurationValidator(platforms, usage);

        Assert.ThrowsException<PlatformRegistrationException>(() =>
            validator.ValidateRegistrations([CreateDefinition()]));
    }

    [TestMethod]
    public void UnknownModelAndEffortAreRejectedBeforeExecution()
    {
        var fake = new FakeAiPlatform(CodexId);
        var validator = CreateValidator(fake);
        var definition = CreateDefinition();

        Assert.ThrowsException<PlatformConfigurationException>(() => validator.ValidateExecution(definition,
            CreateRequest(model: new ModelId("unknown"))));
        Assert.ThrowsException<PlatformConfigurationException>(() => validator.ValidateExecution(definition,
            CreateRequest(effort: new EffortLevel("ultra"))));
        Assert.AreEqual(0, fake.Requests.Count);
    }

    [TestMethod]
    public async Task HealthCheckReportsUnsupportedInstalledModel()
    {
        var fake = new FakeAiPlatform(CodexId)
        {
            Health = PlatformHealth.Available(
            [
                new PlatformModel(new ModelId("another-model"), [Medium])
            ])
        };

        var health = await CreateValidator(fake).CheckHealthAsync(CreateDefinition());

        Assert.AreEqual(PlatformHealthStatus.Misconfigured, health.Status);
    }

    [TestMethod]
    public async Task HealthCheckPreservesMissingExecutableResult()
    {
        var fake = new FakeAiPlatform(CodexId)
        {
            Health = new PlatformHealth(PlatformHealthStatus.ExecutableMissing, "codex.exe fehlt")
        };

        var health = await CreateValidator(fake).CheckHealthAsync(CreateDefinition());

        Assert.AreEqual(PlatformHealthStatus.ExecutableMissing, health.Status);
        Assert.AreEqual("codex.exe fehlt", health.Message);
        Assert.AreEqual(0, fake.Requests.Count);
    }

    [TestMethod]
    public async Task FakePlatformSimulatesParallelRuntimeAndUsageExceededWithPartialChanges()
    {
        var fake = new FakeAiPlatform(CodexId);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        for (var index = 0; index < 2; index++)
        {
            fake.EnqueueExecution(async (_, cancellationToken) =>
            {
                await release.Task.WaitAsync(cancellationToken);
                return new PlatformExecutionResult(PlatformExecutionOutcome.UsageExceeded, 1,
                    "session", "Limit erreicht", mayHavePartialChanges: true);
            });
        }

        var first = fake.ExecuteAsync(CreateRequest());
        var second = fake.ExecuteAsync(CreateRequest());
        await WaitUntilAsync(() => fake.ActiveExecutions == 2);
        release.SetResult(true);

        var results = await Task.WhenAll(first, second);
        Assert.AreEqual(2, fake.MaximumConcurrentExecutions);
        Assert.IsTrue(results.All(result => result.Outcome == PlatformExecutionOutcome.UsageExceeded));
        Assert.IsTrue(results.All(result => result.MayHavePartialChanges));
    }

    [TestMethod]
    public async Task FakeUsageProviderQueuesSnapshotsAndPublishesChanges()
    {
        var fake = new FakeUsageProvider(CodexId);
        var snapshot = new UsageSnapshot(CodexId, DateTimeOffset.UtcNow, "fake", UsageQuality.Aktuell, []);
        UsageReadResult? pushed = null;
        fake.UsageChanged += (_, args) => pushed = args.Result;

        fake.Enqueue(UsageReadResult.Available(snapshot));
        var read = await fake.ReadAsync(forceRefresh: true);
        fake.SetCurrent(read, publishChange: true);

        Assert.AreSame(snapshot, read.Snapshot);
        Assert.AreSame(read, pushed);
        CollectionAssert.AreEqual(new[] { true }, fake.ForceRefreshRequests.ToArray());
    }

    private static PlatformConfigurationValidator CreateValidator(FakeAiPlatform platform) => new(
        new AiPlatformRegistry([platform]),
        new UsageProviderRegistry([new FakeUsageProvider(platform.PlatformId)]));

    private static PlatformDefinition CreateDefinition() => new(CodexId, "codex",
    [
        new PlatformModel(ModelId, [Medium])
    ]);

    private static PlatformExecutionRequest CreateRequest(ModelId? model = null, EffortLevel? effort = null) =>
        new(CodexId, model ?? ModelId, effort ?? Medium, "Arbeite AP ab", "C:\\project");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
