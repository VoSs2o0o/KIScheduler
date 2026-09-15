using KIScheduler.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Domain;

[TestClass]
public sealed class PlatformProfileTests
{
    [TestMethod]
    public void DefaultDirectoriesComeOnlyFromUserProfileAndAreNormalized()
    {
        var userProfile = Path.Combine(Path.GetTempPath(), "profile-root", "..", "profile-root");
        var inheritedCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var inheritedCodexSqliteHome = Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME");
        var inheritedClaudeDirectory = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", Path.Combine(Path.GetTempPath(), "wrong-codex"));
            Environment.SetEnvironmentVariable("CODEX_SQLITE_HOME", Path.Combine(Path.GetTempPath(), "wrong-sqlite"));
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", Path.Combine(Path.GetTempPath(), "wrong-claude"));

            var codex = PlatformProfile.CreateDefault(PlatformProfileId.New(), new("codex"),
                userProfileDirectory: userProfile);
            var claude = PlatformProfile.CreateDefault(PlatformProfileId.New(), new("claude"),
                userProfileDirectory: userProfile);

            Assert.AreEqual(Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                Path.Combine(userProfile, ".codex"))), codex.ConfigurationDirectory);
            Assert.AreEqual(Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                Path.Combine(userProfile, ".claude"))), claude.ConfigurationDirectory);
            Assert.AreEqual(PlatformProfile.DefaultName, codex.Name);
            Assert.AreEqual(PlatformProfile.DefaultDisplayName, codex.DisplayName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", inheritedCodexHome);
            Environment.SetEnvironmentVariable("CODEX_SQLITE_HOME", inheritedCodexSqliteHome);
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", inheritedClaudeDirectory);
        }
    }

    [TestMethod]
    public void ConfigurationDirectoryMustBeAbsoluteAndDoesNotGetCreated()
    {
        Assert.ThrowsException<ArgumentException>(() => new PlatformProfile(PlatformProfileId.New(),
            new("codex"), "second", "Zweitprofil", "relative/path"));

        var path = Path.Combine(Path.GetTempPath(), $"profile-does-not-exist-{Guid.NewGuid():N}");
        _ = new PlatformProfile(PlatformProfileId.New(), new("codex"), "second", "Zweitprofil", path);
        Assert.IsFalse(Directory.Exists(path));
    }

    [TestMethod]
    public void DefaultFlagDoesNotChangeTheStableInternalName()
    {
        var path = Path.Combine(Path.GetTempPath(), "profile-default-rules");
        var promoted = new PlatformProfile(PlatformProfileId.New(), new("codex"), "other",
            "Arbeitsprofil", path, isDefault: true);
        var demoted = new PlatformProfile(PlatformProfileId.New(), new("codex"),
            PlatformProfile.DefaultName, "Persönlich", Path.Combine(path, "default"));

        Assert.AreEqual("other", promoted.Name);
        Assert.IsTrue(promoted.IsDefault);
        Assert.AreEqual(PlatformProfile.DefaultName, demoted.Name);
        Assert.IsFalse(demoted.IsDefault);
    }
}
