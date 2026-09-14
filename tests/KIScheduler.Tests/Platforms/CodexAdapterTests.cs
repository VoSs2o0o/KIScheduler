using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Platforms;
using KIScheduler.Platforms.Codex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Platforms;

[TestClass]
public sealed class CodexAdapterTests
{
    private static readonly PlatformProfileId ProfileId = new(new Guid("33333333-3333-3333-3333-333333333333"));
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ConfigurationReplacesDefaultAppServerArgumentsInsteadOfAppendingThem()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Codex:AppServerArguments:0"] = "custom-app-server",
                ["Codex:AppServerArguments:1"] = "--custom"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddCodexPlatform(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        CodexOptions options = provider.GetRequiredService<IOptions<CodexOptions>>().Value;

        CollectionAssert.AreEqual(new[] { "custom-app-server", "--custom" }, options.AppServerArguments);
    }

    [TestMethod]
    public void JsonlParserExtractsSessionEventsAndFinalMessageAndIgnoresAdditionalFields()
    {
        string[] lines =
        [
            "{\"type\":\"thread.started\",\"thread_id\":\"thread-1\",\"future\":true}",
            "{\"type\":\"turn.started\",\"unknown\":{\"nested\":1}}",
            "{\"type\":\"item.completed\",\"item\":{\"id\":\"i1\",\"type\":\"agent_message\",\"text\":\"Fertig\",\"newField\":42}}",
            "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":1}}"
        ];

        CodexJsonlParseResult result = CodexJsonlParser.Parse(lines);

        Assert.AreEqual("thread-1", result.SessionId);
        Assert.AreEqual("Fertig", result.FinalMessage);
        Assert.AreEqual(4, result.Events.Count);
        Assert.IsNull(result.ParseError);
    }

    [TestMethod]
    public async Task PlatformPassesPromptModelEffortAndClassifiesStructuredQuota()
    {
        var runner = new RecordingRunner(new ProcessRunResult(ProcessTerminationReason.Completed, 1,
            TimeSpan.FromSeconds(1),
            [
                new ProcessOutputLine(1, ProcessOutputStream.StandardOutput,
                    "{\"type\":\"thread.started\",\"thread_id\":\"thread-7\"}", Now),
                new ProcessOutputLine(2, ProcessOutputStream.StandardOutput,
                    "{\"type\":\"error\",\"error\":{\"rateLimitReachedType\":\"primary\",\"message\":\"stopped\"}}", Now)
            ]));
        var platform = new CodexPlatform(runner, Options.Create(new CodexOptions()),
            new FixedProfileResolver(DefaultProfile()));
        var request = new PlatformExecutionRequest(CodexPlatform.Id, ProfileId, new ModelId("gpt-test"),
            new EffortLevel("high"), "Prompt über stdin", Environment.CurrentDirectory);

        PlatformExecutionResult result = await platform.ExecuteAsync(request);

        Assert.AreEqual(PlatformExecutionOutcome.UsageExceeded, result.Outcome);
        Assert.AreEqual(PlatformFailureKind.Quota, result.FailureKind);
        Assert.AreEqual("thread-7", result.SessionId);
        Assert.AreEqual("Prompt über stdin", runner.LastRequest!.StandardInput);
        Assert.IsTrue(runner.LastRequest.SuppressOutputLogging);
        CollectionAssert.Contains(runner.LastRequest.Arguments.ToList(), "gpt-test");
        CollectionAssert.Contains(runner.LastRequest.Arguments.ToList(), "model_reasoning_effort=\"high\"");
        Assert.IsTrue(result.MayHavePartialChanges);
    }

    [TestMethod]
    public async Task PlatformDistinguishesAuthenticationNetworkAndParseErrors()
    {
        await AssertFailureKind("{\"type\":\"error\",\"message\":\"401 unauthorized\"}", "", PlatformFailureKind.Authentication);
        await AssertFailureKind("{\"type\":\"error\",\"message\":\"network error: connection refused\"}", "", PlatformFailureKind.Network);
        await AssertFailureKind("not-json", "", PlatformFailureKind.Parse);
    }

    [TestMethod]
    public async Task PlatformResumesConfiguredSessionWithPromptFromStdin()
    {
        var runner = new RecordingRunner(new ProcessRunResult(ProcessTerminationReason.Completed, 0,
            TimeSpan.Zero,
            [new ProcessOutputLine(1, ProcessOutputStream.StandardOutput,
                "{\"type\":\"turn.completed\"}", Now)]));
        string profileDirectory = Path.Combine(Path.GetTempPath(), "codex-profile-two");
        var profile = new PlatformProfile(ProfileId, CodexPlatform.Id, "codex2", "Codex 2", profileDirectory);
        var platform = new CodexPlatform(runner, Options.Create(new CodexOptions()),
            new FixedProfileResolver(profile));
        var request = new PlatformExecutionRequest(CodexPlatform.Id, ProfileId, new ModelId("model"),
            new EffortLevel("medium"), "weiter", Environment.CurrentDirectory) { SessionId = "session-42" };

        PlatformExecutionResult result = await platform.ExecuteAsync(request);

        Assert.AreEqual(PlatformExecutionOutcome.Succeeded, result.Outcome);
        Assert.AreEqual("session-42", result.SessionId);
        int resumeIndex = runner.LastRequest!.Arguments.ToList().IndexOf("resume");
        Assert.IsTrue(resumeIndex >= 0);
        Assert.AreEqual("session-42", runner.LastRequest.Arguments[resumeIndex + 1]);
        Assert.AreEqual("-", runner.LastRequest.Arguments[resumeIndex + 2]);
        Assert.AreEqual("weiter", runner.LastRequest.StandardInput);
        Assert.AreEqual(profileDirectory, runner.LastRequest.EnvironmentVariables["CODEX_HOME"]);
        Assert.AreEqual(profileDirectory, runner.LastRequest.EnvironmentVariables["CODEX_SQLITE_HOME"]);
    }

    [TestMethod]
    public async Task HealthCheckUsesProfileForVersionAndLoginStatusAndReportsInvalidLogin()
    {
        string profileDirectory = Path.Combine(Path.GetTempPath(), "codex-profile-health");
        var profile = new PlatformProfile(ProfileId, CodexPlatform.Id, "codex2", "Codex 2", profileDirectory);
        var runner = new QueueRunner(
            Completed(0, "codex 1.2.3"),
            Completed(1, stderr: "not logged in"));
        var platform = new CodexPlatform(runner, Options.Create(new CodexOptions()),
            new FixedProfileResolver(profile));

        PlatformHealth health = await platform.CheckAvailabilityAsync(ProfileId);

        Assert.AreEqual(PlatformHealthStatus.Misconfigured, health.Status);
        StringAssert.Contains(health.Message!, "keine gültige CLI-Anmeldung");
        StringAssert.Contains(health.Message!, "dateibasierten Credential-Store");
        Assert.AreEqual(2, runner.Requests.Count);
        CollectionAssert.AreEqual(new[] { "login", "status" }, runner.Requests[1].Arguments.ToArray());
        Assert.IsTrue(runner.Requests.All(request =>
            request.EnvironmentVariables["CODEX_HOME"] == profileDirectory
            && request.EnvironmentVariables["CODEX_SQLITE_HOME"] == profileDirectory));
        Assert.IsTrue(runner.Requests[1].SuppressOutputLogging);
    }

    [TestMethod]
    public void UsageNormalizationPrefersAllMultiBucketsAndPreservesBothWindowsAndMetadata()
    {
        var response = new CodexRateLimitsResponse
        {
            RateLimits = Bucket("fallback", 99, null),
            RateLimitsByLimitId = new Dictionary<string, CodexRateLimitBucket>
            {
                ["codex"] = Bucket("codex", 25, 50, "Codex Standard"),
                ["codex_other"] = Bucket("codex_other", 42, null, "Weitere Nutzung", "secondary")
            }
        };

        UsageReadResult result = CodexUsageProvider.Normalize(response, Now, ProfileId);

        Assert.AreEqual(UsageReadStatus.Available, result.Status);
        Assert.AreEqual(3, result.Snapshot!.Windows.Count);
        Assert.IsFalse(result.Snapshot.Windows.Any(window => window.LimitId == "fallback"));
        UsageWindow reached = result.Snapshot.Windows.Single(window => window.LimitId == "codex_other");
        Assert.AreEqual("Weitere Nutzung", reached.LimitName);
        Assert.AreEqual("secondary", reached.RateLimitReachedType);
        Assert.IsTrue(reached.IsServerLimitReached);
        Assert.AreEqual(TimeSpan.FromMinutes(300), reached.WindowDuration);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1893456000), reached.ResetAtUtc);
    }

    [TestMethod]
    public void UsageNormalizationReportsMissingRequiredWindowFields()
    {
        var response = new CodexRateLimitsResponse
        {
            RateLimits = new CodexRateLimitBucket
            {
                LimitId = "codex",
                Primary = new CodexRateLimitWindow { UsedPercent = 10 }
            }
        };

        UsageReadResult result = CodexUsageProvider.Normalize(response, Now, ProfileId);

        Assert.AreEqual(UsageReadStatus.Available, result.Status);
        StringAssert.Contains(result.Message!, "resetsAt");
        StringAssert.Contains(result.Message!, "windowDurationMins");
    }

    [TestMethod]
    public async Task ProviderUsesCacheHonorsForceRefreshAndPublishesPushUpdates()
    {
        var client = new FakeAppServerClient(BucketResponse(10), ProfileId);
        var clock = new TestClock(Now);
        using var provider = new CodexUsageProvider(client, clock, Options.Create(new CodexOptions
        {
            UsageCacheDuration = TimeSpan.FromMinutes(1)
        }));
        UsageReadResult? pushed = null;
        provider.UsageChanged += (_, args) => pushed = args.Result;

        _ = await provider.ReadAsync(false);
        _ = await provider.ReadAsync(false);
        Assert.AreEqual(1, client.ReadCount);

        _ = await provider.ReadAsync(true);
        Assert.AreEqual(2, client.ReadCount);

        client.Push(BucketResponse(77));
        Assert.AreEqual(77m, pushed!.Snapshot!.Windows.Single().UsedPercent.Value);
        Assert.AreEqual(77m, (await provider.ReadAsync(false)).Snapshot!.Windows.Single().UsedPercent.Value);
    }

    [TestMethod]
    public async Task ProviderMapsAppServerAuthenticationFailureToUnknown()
    {
        var client = new FakeAppServerClient(BucketResponse(10), ProfileId)
        {
            Failure = new CodexAppServerException(CodexAppServerFailureKind.Authentication, "login required")
        };
        using var provider = new CodexUsageProvider(client, new TestClock(Now), Options.Create(new CodexOptions()));

        UsageReadResult result = await provider.ReadAsync(true);

        Assert.AreEqual(UsageReadStatus.Unknown, result.Status);
        StringAssert.Contains(result.Message!, "authentication");
    }

    [TestMethod]
    public async Task ProviderKeepsCachesAndPushUpdatesSeparatedByProfile()
    {
        PlatformProfileId secondId = new(new Guid("44444444-4444-4444-4444-444444444444"));
        var first = new FakeAppServerClient(BucketResponse(10), ProfileId);
        var second = new FakeAppServerClient(BucketResponse(20), secondId);
        var profiles = new FixedProfileResolver(
            new PlatformProfile(ProfileId, CodexPlatform.Id, "codex1", "Codex 1", "C:\\profiles\\codex1"),
            new PlatformProfile(secondId, CodexPlatform.Id, "codex2", "Codex 2", "C:\\profiles\\codex2"));
        var factory = new FakeClientFactory(first, second);
        using var provider = new CodexUsageProvider(factory, profiles, new TestClock(Now),
            Options.Create(new CodexOptions { UsageCacheDuration = TimeSpan.FromMinutes(1) }));
        IUsageProvider usageProvider = provider;
        UsageChangedEventArgs? pushed = null;
        provider.UsageChanged += (_, args) => pushed = args;

        UsageReadResult firstRead = await usageProvider.ReadAsync(ProfileId, false);
        UsageReadResult secondRead = await usageProvider.ReadAsync(secondId, false);
        first.Push(BucketResponse(77));

        Assert.AreEqual(ProfileId, firstRead.PlatformProfileId);
        Assert.AreEqual(secondId, secondRead.PlatformProfileId);
        Assert.AreEqual(ProfileId, pushed!.PlatformProfileId);
        Assert.AreEqual(77m, pushed.Result.Snapshot!.Windows.Single().UsedPercent.Value);
        Assert.AreEqual(77m, (await provider.ReadAsync(ProfileId, false)).Snapshot!.Windows.Single().UsedPercent.Value);
        Assert.AreEqual(20m, (await provider.ReadAsync(secondId, false)).Snapshot!.Windows.Single().UsedPercent.Value);
        Assert.AreEqual(1, first.ReadCount);
        Assert.AreEqual(1, second.ReadCount);
    }

    [TestMethod]
    public async Task AppServerTransportPerformsHandshakeReadsLimitsAndReceivesPush()
    {
        string profileDirectory = Path.Combine(Path.GetTempPath(), $"codex-app-server-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);
        var options = new CodexOptions
        {
            Executable = "dotnet",
            AppServerRequestTimeout = TimeSpan.FromSeconds(5),
            AppServerArguments =
            [
                typeof(KIScheduler.ProcessTestHelper.Marker).Assembly.Location,
                "fake-codex-app-server"
            ]
        };
        var profile = new PlatformProfile(ProfileId, CodexPlatform.Id, "codex2", "Codex 2", profileDirectory);
        await using var client = new CodexAppServerClient(profile, Options.Create(options),
            NullLogger<CodexAppServerClient>.Instance);
        var pushed = new TaskCompletionSource<CodexRateLimitsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        PlatformProfileId? pushedProfile = null;
        client.RateLimitsChanged += (_, args) =>
        {
            pushedProfile = args.PlatformProfileId;
            pushed.TrySetResult(args.RateLimits);
        };

        CodexRateLimitsResponse response = await client.ReadRateLimitsAsync();
        CodexRateLimitsResponse update = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(25m, response.RateLimitsByLimitId!["codex"].Primary!.UsedPercent);
        Assert.AreEqual(31m, update.RateLimits!.Primary!.UsedPercent);
        Assert.AreEqual(ProfileId, pushedProfile);
        Assert.AreEqual(profileDirectory, response.AdditionalFields!["profileHome"].GetString());
        Assert.AreEqual(profileDirectory, response.AdditionalFields!["sqliteHome"].GetString());
        Directory.Delete(profileDirectory);
    }

    [TestMethod]
    public async Task AppServerFactoryReusesPerProfileAndStopsClientsOnChangeAndDisable()
    {
        string profileDirectory = Path.Combine(Path.GetTempPath(), $"codex-factory-{Guid.NewGuid():N}");
        string changedDirectory = Path.Combine(Path.GetTempPath(), $"codex-factory-changed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(profileDirectory);
        Directory.CreateDirectory(changedDirectory);
        try
        {
            var profile = new PlatformProfile(ProfileId, CodexPlatform.Id, "codex2", "Codex 2", profileDirectory);
            var resolver = new FixedProfileResolver(profile);
            await using var factory = new CodexAppServerClientFactory(resolver,
                Options.Create(new CodexOptions()), NullLogger<CodexAppServerClient>.Instance);

            ICodexAppServerClient first = await factory.GetAsync(ProfileId);
            ICodexAppServerClient second = await factory.GetAsync(ProfileId);
            resolver.Set(new PlatformProfile(ProfileId, CodexPlatform.Id, "codex2", "Codex 2", changedDirectory));
            ICodexAppServerClient replacement = await factory.GetAsync(ProfileId);

            Assert.AreSame(first, second);
            Assert.AreNotSame(first, replacement);
            Assert.AreEqual(ProfileId, replacement.PlatformProfileId);
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => first.ReadRateLimitsAsync());

            resolver.Failure = new CodexProfileException(CodexProfileFailureKind.Disabled,
                "Das Profil ist deaktiviert.");
            await Assert.ThrowsExceptionAsync<CodexProfileException>(() => factory.GetAsync(ProfileId));
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => replacement.ReadRateLimitsAsync());
        }
        finally
        {
            Directory.Delete(profileDirectory);
            Directory.Delete(changedDirectory);
        }
    }

    [TestMethod]
    public async Task ProfileResolverDiagnosesMissingFolderWithoutReadingCredentialFiles()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"missing-codex-profile-{Guid.NewGuid():N}");
        var profile = new PlatformProfile(ProfileId, CodexPlatform.Id, "codex2", "Codex 2", missing);
        var resolver = new CodexProfileResolver(new ProfileRepository(profile));

        CodexProfileException exception = await Assert.ThrowsExceptionAsync<CodexProfileException>(
            () => resolver.ResolveAsync(ProfileId));

        Assert.AreEqual(CodexProfileFailureKind.DirectoryMissing, exception.Kind);
        StringAssert.Contains(exception.Message, "fehlt");
    }

    private static async Task AssertFailureKind(string stdout, string stderr, PlatformFailureKind expected)
    {
        var output = new List<ProcessOutputLine>
        {
            new(1, ProcessOutputStream.StandardOutput, stdout, Now)
        };
        if (stderr.Length > 0) output.Add(new(2, ProcessOutputStream.StandardError, stderr, Now));
        var runner = new RecordingRunner(new ProcessRunResult(ProcessTerminationReason.Completed, 1,
            TimeSpan.Zero, output));
        var platform = new CodexPlatform(runner, Options.Create(new CodexOptions()),
            new FixedProfileResolver(DefaultProfile()));
        PlatformExecutionResult result = await platform.ExecuteAsync(new PlatformExecutionRequest(CodexPlatform.Id,
            ProfileId, new ModelId("model"), new EffortLevel("medium"), "prompt", Environment.CurrentDirectory));
        Assert.AreEqual(expected, result.FailureKind);
    }

    private static CodexRateLimitsResponse BucketResponse(decimal used) => new()
    {
        RateLimits = Bucket("codex", used, null)
    };

    private static CodexRateLimitBucket Bucket(string id, decimal primary, decimal? secondary,
        string? name = null, string? reached = null) => new()
    {
        LimitId = id,
        LimitName = name,
        RateLimitReachedType = reached,
        Primary = new CodexRateLimitWindow
        {
            UsedPercent = primary,
            WindowDurationMins = 300,
            ResetsAt = 1893456000
        },
        Secondary = secondary.HasValue ? new CodexRateLimitWindow
        {
            UsedPercent = secondary,
            WindowDurationMins = 10080,
            ResetsAt = 1894060800
        } : null
    };

    private sealed class RecordingRunner(ProcessRunResult result) : IProcessRunner
    {
        public ProcessRunRequest? LastRequest { get; private set; }
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class QueueRunner(params ProcessRunResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessRunResult> results = new(results);
        public List<ProcessRunRequest> Requests { get; } = [];

        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class FakeAppServerClient(CodexRateLimitsResponse response,
        PlatformProfileId? profileId = null) : ICodexAppServerClient
    {
        public PlatformProfileId? PlatformProfileId { get; } = profileId;
        public int ReadCount { get; private set; }
        public CodexAppServerException? Failure { get; init; }
        public event EventHandler<CodexRateLimitsChangedEventArgs>? RateLimitsChanged;

        public Task<CodexRateLimitsResponse> ReadRateLimitsAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Failure is null ? Task.FromResult(response) : Task.FromException<CodexRateLimitsResponse>(Failure);
        }

        public void Push(CodexRateLimitsResponse update) =>
            RateLimitsChanged?.Invoke(this, new CodexRateLimitsChangedEventArgs(PlatformProfileId, update));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeClientFactory(params FakeAppServerClient[] clients) : ICodexAppServerClientFactory
    {
        private readonly Dictionary<PlatformProfileId, ICodexAppServerClient> clients = clients
            .ToDictionary(client => client.PlatformProfileId!.Value,
                client => (ICodexAppServerClient)client);

        public Task<ICodexAppServerClient> GetAsync(PlatformProfileId profileId,
            CancellationToken cancellationToken = default) => Task.FromResult(clients[profileId]);
        public Task InvalidateAsync(PlatformProfileId profileId) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedProfileResolver(params PlatformProfile[] profiles) : ICodexProfileResolver
    {
        private readonly Dictionary<PlatformProfileId, PlatformProfile> profiles = profiles.ToDictionary(x => x.Id);
        public CodexProfileException? Failure { get; set; }
        public Task<PlatformProfile> ResolveAsync(PlatformProfileId profileId,
            CancellationToken cancellationToken = default) => Failure is null
                ? Task.FromResult(profiles[profileId])
                : Task.FromException<PlatformProfile>(Failure);
        public Task<PlatformProfile> ResolveDefaultAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(profiles.Values.First());
        public void Set(PlatformProfile profile) => profiles[profile.Id] = profile;
    }

    private sealed class ProfileRepository(PlatformProfile profile) : IPlatformProfileRepository
    {
        public Task<PlatformProfile?> GetAsync(PlatformProfileId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PlatformProfile?>(id == profile.Id ? profile : null);
        public Task<PlatformProfile?> GetDefaultAsync(PlatformId platformId,
            CancellationToken cancellationToken = default) => Task.FromResult<PlatformProfile?>(profile);
        public Task<IReadOnlyList<PlatformProfile>> ListAsync(PlatformId? platformId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlatformProfile>>([profile]);
        public Task SaveAsync(PlatformProfile value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<bool> DisableAsync(PlatformProfileId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private static ProcessRunResult Completed(int exitCode, string? stdout = null, string? stderr = null)
    {
        var lines = new List<ProcessOutputLine>();
        if (stdout is not null) lines.Add(new(1, ProcessOutputStream.StandardOutput, stdout, Now));
        if (stderr is not null) lines.Add(new(2, ProcessOutputStream.StandardError, stderr, Now));
        return new ProcessRunResult(ProcessTerminationReason.Completed, exitCode, TimeSpan.Zero, lines);
    }

    private static PlatformProfile DefaultProfile() => PlatformProfile.CreateDefault(ProfileId,
        CodexPlatform.Id, userProfileDirectory: Path.GetTempPath());

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
