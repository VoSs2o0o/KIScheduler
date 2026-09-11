using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Platforms.Claude;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Platforms;

[TestClass]
public sealed class ClaudeAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task PlatformUsesNonInteractiveStructuredOutputStdinModelEffortAndResume()
    {
        var runner = new RecordingRunner(Completed(0,
            "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"session-7\"}",
            "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"Fertig\",\"session_id\":\"session-7\"}"));
        var platform = new ClaudePlatform(runner, Options.Create(new ClaudeOptions()));
        var request = new PlatformExecutionRequest(ClaudePlatform.Id, new ModelId("claude-sonnet-5"),
            new EffortLevel("high"), "Prompt über stdin", Environment.CurrentDirectory)
        {
            SessionId = "session-7"
        };

        PlatformExecutionResult result = await platform.ExecuteAsync(request);

        Assert.AreEqual(PlatformExecutionOutcome.Succeeded, result.Outcome);
        Assert.AreEqual("session-7", result.SessionId);
        Assert.AreEqual("Fertig", result.Message);
        Assert.AreEqual("Prompt über stdin", runner.LastRequest!.StandardInput);
        Assert.IsTrue(runner.LastRequest.SuppressOutputLogging);
        CollectionAssert.Contains(runner.LastRequest.Arguments.ToList(), "--print");
        AssertArgumentValue(runner.LastRequest.Arguments, "--output-format", "stream-json");
        AssertArgumentValue(runner.LastRequest.Arguments, "--model", "claude-sonnet-5");
        AssertArgumentValue(runner.LastRequest.Arguments, "--effort", "high");
        AssertArgumentValue(runner.LastRequest.Arguments, "--resume", "session-7");
    }

    [TestMethod]
    public async Task PlatformClassifiesUsageLimitFromStderrSeparately()
    {
        var runner = new RecordingRunner(new ProcessRunResult(ProcessTerminationReason.Completed, 1,
            TimeSpan.Zero,
            [new ProcessOutputLine(1, ProcessOutputStream.StandardError,
                "You've hit your limit · resets 2am", Now)]));
        var platform = new ClaudePlatform(runner, Options.Create(new ClaudeOptions()));

        PlatformExecutionResult result = await platform.ExecuteAsync(Request());

        Assert.AreEqual(PlatformExecutionOutcome.UsageExceeded, result.Outcome);
        Assert.AreEqual(PlatformFailureKind.Quota, result.FailureKind);
        Assert.IsTrue(result.MayHavePartialChanges);
        Assert.AreEqual(1, result.ExitCode);
    }

    [TestMethod]
    public async Task PlatformDistinguishesAuthenticationNetworkAndMalformedJson()
    {
        await AssertFailureKind("{\"type\":\"result\",\"is_error\":true,\"result\":\"authentication_error: login required\"}",
            PlatformFailureKind.Authentication);
        await AssertFailureKind("{\"type\":\"error\",\"message\":\"network error: connection refused\"}",
            PlatformFailureKind.Network);
        await AssertFailureKind("not-json", PlatformFailureKind.Parse);
    }

    [TestMethod]
    public async Task AvailabilityReportsMissingExecutableWithoutRealClaudeInstallation()
    {
        var runner = new RecordingRunner(new ProcessRunResult(ProcessTerminationReason.StartFailed, null,
            TimeSpan.Zero, [], "not found"));
        var platform = new ClaudePlatform(runner, Options.Create(new ClaudeOptions()));

        PlatformHealth health = await platform.CheckAvailabilityAsync();

        Assert.AreEqual(PlatformHealthStatus.ExecutableMissing, health.Status);
        CollectionAssert.AreEqual(new[] { "--version" }, runner.LastRequest!.Arguments.ToArray());
    }

    [TestMethod]
    [DataRow("Current session: 0% used", 0)]
    [DataRow("header\r\n  Current session:   100%   used  \r\nfooter", 100)]
    [DataRow("Current SESSION:\t42%\nused", 42)]
    public void StandardRegexAcceptsBoundaryPercentAndWhitespace(string sample, int expected)
    {
        ClaudeUsageTestResult test = Provider().TestSample(sample);

        Assert.IsNotNull(test.MatchedText);
        Assert.AreEqual(UsageReadStatus.Available, test.NormalizedResult.Status);
        Assert.AreEqual(expected,
            test.NormalizedResult.Snapshot!.Windows.Single().UsedPercent.Value);
    }

    [TestMethod]
    public void OutOfRangeAndNoMatchAreUnknownInsteadOfZero()
    {
        ClaudeUsageProvider provider = Provider();

        ClaudeUsageTestResult outOfRange = provider.TestSample("Current session: 101% used");
        ClaudeUsageTestResult noMatch = provider.TestSample("Usage currently unavailable");

        Assert.AreEqual(UsageReadStatus.Unknown, outOfRange.NormalizedResult.Status);
        Assert.IsNull(outOfRange.NormalizedResult.Snapshot);
        Assert.AreEqual(UsageReadStatus.Unknown, noMatch.NormalizedResult.Status);
        Assert.IsNull(noMatch.NormalizedResult.Snapshot);
    }

    [TestMethod]
    public void CultureAndFractionUnitAreConfigurable()
    {
        ClaudeUsageProvider provider = Provider(new ClaudeUsageOptions
        {
            Pattern = @"used=(?<used>\d+,\d+)",
            Culture = "de-DE",
            Unit = RegexValueUnit.Fraction
        });

        ClaudeUsageTestResult test = provider.TestSample("used=0,75");

        Assert.AreEqual(75m, test.NormalizedResult.Snapshot!.Windows.Single().UsedPercent.Value);
    }

    [TestMethod]
    public void ResetCanComeFromNamedGroupOrExplicitConfiguration()
    {
        DateTimeOffset explicitReset = Now.AddHours(4);
        ClaudeUsageTestResult named = Provider(new ClaudeUsageOptions
        {
            Pattern = @"Current session:\s*(?<used>\d+)% used; reset=(?<reset>[^;]+)",
            ResetFormat = "yyyy-MM-dd HH:mm zzz"
        }).TestSample("Current session: 30% used; reset=2026-09-12 01:30 +02:00");
        ClaudeUsageTestResult configured = Provider(new ClaudeUsageOptions
        {
            ConfiguredResetAtUtc = explicitReset,
            ResetGroupName = null
        }).TestSample("Current session: 30% used");

        Assert.AreEqual(new DateTimeOffset(2026, 9, 11, 23, 30, 0, TimeSpan.Zero),
            named.NormalizedResult.Snapshot!.Windows.Single().ResetAtUtc);
        Assert.AreEqual(explicitReset, configured.NormalizedResult.Snapshot!.Windows.Single().ResetAtUtc);
    }

    [TestMethod]
    public void MissingResetLeavesResetNullSoNoEndSprintCanBeDerived()
    {
        ClaudeUsageTestResult test = Provider().TestSample("Current session: 30% used");

        Assert.IsNull(test.NormalizedResult.Snapshot!.Windows.Single().ResetAtUtc);
    }

    [TestMethod]
    public void RegexEvaluationHasEnforcedTimeout()
    {
        string pathologicalInput = new('a', 100_000);
        pathologicalInput += "!";
        ClaudeUsageProvider provider = Provider(new ClaudeUsageOptions
        {
            Pattern = @"^(a+)+(?<used>\d+)$",
            RegexTimeout = TimeSpan.FromMilliseconds(1)
        });

        ClaudeUsageTestResult test = provider.TestSample(pathologicalInput);

        Assert.AreEqual(UsageReadStatus.Unknown, test.NormalizedResult.Status);
        StringAssert.Contains(test.NormalizedResult.Message!, "Zeitlimit");
    }

    [TestMethod]
    public async Task ExecutionAndUsageUseIndependentCommandsAndCanBeTestedSeparately()
    {
        var runner = new QueueRunner(
            Completed(0, "Current session: 62% used"),
            Completed(0, "{\"type\":\"result\",\"is_error\":false,\"result\":\"ok\",\"session_id\":\"s1\"}"));
        var options = Options.Create(new ClaudeOptions
        {
            Executable = "claude-execution",
            Usage = new ClaudeUsageOptions { Executable = "usage-probe" }
        });
        var usage = new ClaudeUsageProvider(new CommandRegexReader(runner), new TestClock(), options);
        var platform = new ClaudePlatform(runner, options);

        UsageReadResult usageResult = await usage.ReadAsync(true);
        PlatformExecutionResult executionResult = await platform.ExecuteAsync(Request());

        Assert.AreEqual(UsageReadStatus.Available, usageResult.Status);
        Assert.AreEqual(PlatformExecutionOutcome.Succeeded, executionResult.Outcome);
        CollectionAssert.AreEqual(new[] { "usage-probe", "claude-execution" }, runner.Executables.ToArray());
    }

    private static ClaudeUsageProvider Provider(ClaudeUsageOptions? usage = null)
    {
        var runner = new RecordingRunner(Completed(0));
        var options = Options.Create(new ClaudeOptions { Usage = usage ?? new ClaudeUsageOptions() });
        return new ClaudeUsageProvider(new CommandRegexReader(runner), new TestClock(), options);
    }

    private static PlatformExecutionRequest Request() => new(ClaudePlatform.Id, new ModelId("sonnet"),
        new EffortLevel("medium"), "prompt", Environment.CurrentDirectory);

    private static ProcessRunResult Completed(int exitCode, params string[] stdout) => new(
        ProcessTerminationReason.Completed, exitCode, TimeSpan.Zero,
        stdout.Select((text, index) => new ProcessOutputLine(index + 1,
            ProcessOutputStream.StandardOutput, text, Now)).ToArray());

    private static void AssertArgumentValue(IReadOnlyList<string> arguments, string name, string value)
    {
        int index = arguments.ToList().IndexOf(name);
        Assert.IsTrue(index >= 0, $"Argument {name} fehlt.");
        Assert.AreEqual(value, arguments[index + 1]);
    }

    private static async Task AssertFailureKind(string stdout, PlatformFailureKind expected)
    {
        var runner = new RecordingRunner(Completed(1, stdout));
        var platform = new ClaudePlatform(runner, Options.Create(new ClaudeOptions()));

        PlatformExecutionResult result = await platform.ExecuteAsync(Request());

        Assert.AreEqual(expected, result.FailureKind);
        Assert.AreEqual(PlatformExecutionOutcome.HumanReviewRequired, result.Outcome);
    }

    private sealed class RecordingRunner(ProcessRunResult result) : IProcessRunner
    {
        public ProcessRunRequest? LastRequest { get; private set; }
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class QueueRunner(params ProcessRunResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessRunResult> results = new(results);
        public List<string> Executables { get; } = [];
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            Executables.Add(request.FileName);
            return Task.FromResult(results.Dequeue());
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
