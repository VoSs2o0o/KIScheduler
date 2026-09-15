using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Infrastructure.Processes;
using KIScheduler.Platforms.Claude;
using KIScheduler.Platforms.Codex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Platforms;

[TestClass]
[DoNotParallelize]
public sealed class ProfileEndToEndTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static string HelperExecutable => Path.ChangeExtension(
        typeof(KIScheduler.ProcessTestHelper.Marker).Assembly.Location, ".exe");

    [TestMethod]
    public async Task TwoCodexProfilesKeepExecutionUsageAndInheritedProviderEnvironmentSeparated()
    {
        string root = CreateProfileRoot("codex");
        string firstDirectory = CreateProfile(root, "default", 11, "auth.json", "codex-secret-one");
        string secondDirectory = CreateProfile(root, "codex2", 67, "auth.json", "codex-secret-two");
        string? inheritedHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string? inheritedSqlite = Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME");
        Environment.SetEnvironmentVariable("CODEX_HOME", Path.Combine(root, "wrong-home"));
        Environment.SetEnvironmentVariable("CODEX_SQLITE_HOME", Path.Combine(root, "wrong-sqlite"));

        try
        {
            var first = PlatformProfile.CreateDefault(PlatformProfileId.New(), CodexPlatform.Id,
                userProfileDirectory: root);
            first = new PlatformProfile(first.Id, first.PlatformId, first.Name, first.DisplayName,
                firstDirectory, isDefault: true);
            var second = new PlatformProfile(PlatformProfileId.New(), CodexPlatform.Id, "codex2", "codex2",
                secondDirectory);
            var repository = new MemoryProfileRepository(first, second);
            var resolver = new CodexProfileResolver(repository);
            var runner = new LocalProcessRunner(NullLogger<LocalProcessRunner>.Instance);
            var options = Options.Create(new CodexOptions
            {
                Executable = HelperExecutable,
                AppServerArguments = ["fake-codex-app-server"],
                UsageCacheDuration = TimeSpan.Zero
            });
            await using var clients = new CodexAppServerClientFactory(resolver, options,
                NullLogger<CodexAppServerClient>.Instance);
            using var usage = new CodexUsageProvider(clients, resolver, new FixedClock(), options);
            var platform = new CodexPlatform(runner, options, resolver);

            UsageReadResult firstUsage = await usage.ReadAsync(first.Id, true);
            UsageReadResult secondUsage = await usage.ReadAsync(second.Id, true);
            PlatformExecutionResult firstRun = await ExecuteCodexAsync(platform, first.Id, "prompt-one");
            PlatformExecutionResult secondRun = await ExecuteCodexAsync(platform, second.Id, "prompt-two");

            Assert.AreEqual(11m, firstUsage.Snapshot!.Windows.Single().UsedPercent.Value);
            Assert.AreEqual(67m, secondUsage.Snapshot!.Windows.Single().UsedPercent.Value);
            Assert.AreEqual(first.Id, firstUsage.PlatformProfileId);
            Assert.AreEqual(second.Id, secondUsage.PlatformProfileId);
            AssertExecution(firstRun, firstDirectory, "prompt-one", "auth.json", "codex-secret-one");
            AssertExecution(secondRun, secondDirectory, "prompt-two", "auth.json", "codex-secret-two");
            StringAssert.Contains(firstRun.Message!, $"{firstDirectory}|{firstDirectory}");
            StringAssert.Contains(secondRun.Message!, $"{secondDirectory}|{secondDirectory}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", inheritedHome);
            Environment.SetEnvironmentVariable("CODEX_SQLITE_HOME", inheritedSqlite);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task TwoClaudeProfilesKeepExecutionAndUsageResponsesSeparated()
    {
        string root = CreateProfileRoot("claude");
        string firstDirectory = CreateProfile(root, "default", 23, ".credentials.json", "claude-secret-one");
        string secondDirectory = CreateProfile(root, "claude2", 81, ".credentials.json", "claude-secret-two");
        string? inheritedDirectory = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", Path.Combine(root, "wrong-claude"));

        try
        {
            var first = new PlatformProfile(PlatformProfileId.New(), ClaudePlatform.Id,
                PlatformProfile.DefaultName, PlatformProfile.DefaultDisplayName, firstDirectory, isDefault: true);
            var second = new PlatformProfile(PlatformProfileId.New(), ClaudePlatform.Id, "claude2", "claude2",
                secondDirectory);
            var repository = new MemoryProfileRepository(first, second);
            var resolver = new ClaudeProfileResolver(repository);
            var runner = new LocalProcessRunner(NullLogger<LocalProcessRunner>.Instance);
            var options = Options.Create(new ClaudeOptions
            {
                Executable = HelperExecutable,
                Usage = new ClaudeUsageOptions
                {
                    Executable = HelperExecutable,
                    Arguments = ["fake-claude-usage"]
                }
            });
            var usage = new ClaudeUsageProvider(new CommandRegexReader(runner), resolver, new FixedClock(), options);
            var platform = new ClaudePlatform(runner, options, resolver);

            UsageReadResult firstUsage = await usage.ReadAsync(first.Id, true);
            UsageReadResult secondUsage = await usage.ReadAsync(second.Id, true);
            PlatformExecutionResult firstRun = await ExecuteClaudeAsync(platform, first.Id, "prompt-one");
            PlatformExecutionResult secondRun = await ExecuteClaudeAsync(platform, second.Id, "prompt-two");

            Assert.AreEqual(23m, firstUsage.Snapshot!.Windows.Single().UsedPercent.Value);
            Assert.AreEqual(81m, secondUsage.Snapshot!.Windows.Single().UsedPercent.Value);
            Assert.AreEqual(first.Id, firstUsage.PlatformProfileId);
            Assert.AreEqual(second.Id, secondUsage.PlatformProfileId);
            AssertExecution(firstRun, firstDirectory, "prompt-one", ".credentials.json", "claude-secret-one");
            AssertExecution(secondRun, secondDirectory, "prompt-two", ".credentials.json", "claude-secret-two");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", inheritedDirectory);
            Directory.Delete(root, recursive: true);
        }
    }

    private static Task<PlatformExecutionResult> ExecuteCodexAsync(CodexPlatform platform,
        PlatformProfileId profileId, string prompt) => platform.ExecuteAsync(new PlatformExecutionRequest(
        CodexPlatform.Id, profileId, new("gpt-test"), new("medium"), prompt, Environment.CurrentDirectory));

    private static Task<PlatformExecutionResult> ExecuteClaudeAsync(ClaudePlatform platform,
        PlatformProfileId profileId, string prompt) => platform.ExecuteAsync(new PlatformExecutionRequest(
        ClaudePlatform.Id, profileId, new("sonnet-test"), new("medium"), prompt, Environment.CurrentDirectory));

    private static void AssertExecution(PlatformExecutionResult result, string expectedDirectory,
        string expectedPrompt, string credentialFileName, string credentialSecret)
    {
        Assert.AreEqual(PlatformExecutionOutcome.Succeeded, result.Outcome);
        StringAssert.Contains(result.Message!, expectedDirectory);
        StringAssert.Contains(result.Message!, expectedPrompt);
        Assert.IsFalse(result.Message!.Contains(credentialSecret, StringComparison.Ordinal));
        Assert.AreEqual($"{{\"token\":\"{credentialSecret}\"}}",
            File.ReadAllText(Path.Combine(expectedDirectory, credentialFileName)));
    }

    private static string CreateProfileRoot(string platform)
    {
        string root = Path.Combine(Path.GetTempPath(), $"kischeduler-ap18-{platform}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateProfile(string root, string name, int usage, string credentialFileName,
        string credentialSecret)
    {
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".fake-usage-percent"),
            usage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(directory, credentialFileName),
            $"{{\"token\":\"{credentialSecret}\"}}");
        return directory;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class MemoryProfileRepository(params PlatformProfile[] profiles) : IPlatformProfileRepository
    {
        private readonly Dictionary<PlatformProfileId, PlatformProfile> values = profiles.ToDictionary(x => x.Id);

        public Task<PlatformProfile?> GetAsync(PlatformProfileId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.GetValueOrDefault(id));

        public Task<PlatformProfile?> GetDefaultAsync(PlatformId platformId,
            CancellationToken cancellationToken = default) => Task.FromResult(values.Values.SingleOrDefault(x =>
                x.Enabled && x.IsDefault && x.PlatformId.Value.Equals(platformId.Value,
                    StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<PlatformProfile>> ListAsync(PlatformId? platformId = null,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PlatformProfile>>(
            values.Values.Where(x => platformId is null || x.PlatformId.Value.Equals(platformId.Value,
                StringComparison.OrdinalIgnoreCase)).ToList());

        public Task SaveAsync(PlatformProfile profile, CancellationToken cancellationToken = default)
        {
            values[profile.Id] = profile;
            return Task.CompletedTask;
        }

        public Task SetDefaultAsync(PlatformProfileId id, CancellationToken cancellationToken = default)
        {
            if (!values.TryGetValue(id, out var selected) || !selected.Enabled)
                throw new InvalidOperationException();
            foreach (var profile in values.Values.Where(x => x.PlatformId == selected.PlatformId).ToList())
                values[profile.Id] = new PlatformProfile(profile.Id, profile.PlatformId, profile.Name,
                    profile.DisplayName, profile.ConfigurationDirectory, profile.Enabled,
                    profile.Id == id, profile.ShowUsageInStatusBar);
            return Task.CompletedTask;
        }

        public Task<bool> DisableAsync(PlatformProfileId id, CancellationToken cancellationToken = default)
        {
            if (!values.TryGetValue(id, out PlatformProfile? profile)) return Task.FromResult(false);
            values[id] = new PlatformProfile(profile.Id, profile.PlatformId, profile.Name, profile.DisplayName,
                profile.ConfigurationDirectory, enabled: false, profile.IsDefault, profile.ShowUsageInStatusBar);
            return Task.FromResult(true);
        }
    }
}
