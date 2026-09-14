using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace KIScheduler.ProcessTestHelper;

public sealed class Marker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            return 64;
        }

        switch (args[0])
        {
            case "exec":
                return await RunFakeCodexExecutionAsync();

            case "--print":
                return await RunFakeClaudeExecutionAsync();

            case "echo":
                Console.WriteLine($"cwd:{Environment.CurrentDirectory}");
                for (var index = 1; index < args.Length; index++)
                {
                    Console.WriteLine($"arg:{index - 1}:{args[index]}");
                }

                Console.WriteLine($"stdin:{await Console.In.ReadToEndAsync()}");
                return 0;

            case "exit":
                Console.Out.WriteLine("requested exit");
                Console.Error.WriteLine("requested error output");
                return int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);

            case "environment":
                Console.WriteLine(Environment.GetEnvironmentVariable(args[1]) ?? "<null>");
                return 0;

            case "flood":
                int count = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
                string payload = new('x', 256);
                Task stdout = Task.Run(() => WriteLines(Console.Out, "out", count, payload));
                Task stderr = Task.Run(() => WriteLines(Console.Error, "err", count, payload));
                await Task.WhenAll(stdout, stderr);
                return 0;

            case "sleep":
                await Task.Delay(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture));
                return 0;

            case "spawn-child":
                using (Process child = StartChild(args[1]))
                {
                    Console.WriteLine($"child:{child.Id}");
                    await Console.Out.FlushAsync();
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }

                return 0;

            case "fake-codex-app-server":
                return await RunFakeCodexAppServerAsync();

            case "fake-claude-usage":
                Console.WriteLine($"Current session: {ReadFakeUsage("CLAUDE_CONFIG_DIR")}% used");
                return 0;

            case "child":
                await File.WriteAllTextAsync(args[1], Environment.ProcessId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
                await Task.Delay(TimeSpan.FromMinutes(5));
                return 0;

            default:
                return 65;
        }
    }

    private static async Task<int> RunFakeCodexAppServerAsync()
    {
        bool initialized = false;
        while (await Console.In.ReadLineAsync() is { } line)
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            string? method = root.GetProperty("method").GetString();
            if (method == "initialized")
            {
                initialized = true;
                continue;
            }
            if (!root.TryGetProperty("id", out JsonElement id)) continue;

            if (method == "initialize")
            {
                Console.WriteLine($"{{\"id\":{id.GetRawText()},\"result\":{{\"userAgent\":\"fake\",\"future\":true}}}}");
            }
            else if (method == "account/rateLimits/read")
            {
                if (!initialized)
                {
                    Console.WriteLine($"{{\"id\":{id.GetRawText()},\"error\":{{\"code\":-32000,\"message\":\"Not initialized\"}}}}");
                    await Console.Out.FlushAsync();
                    continue;
                }
                string profileHome = JsonSerializer.Serialize(Environment.GetEnvironmentVariable("CODEX_HOME"));
                string sqliteHome = JsonSerializer.Serialize(Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME"));
                int usedPercent = ReadFakeUsage("CODEX_HOME");
                Console.WriteLine($"{{\"id\":{id.GetRawText()},\"result\":{{\"rateLimitsByLimitId\":{{\"codex\":{{\"limitId\":\"codex\",\"limitName\":\"Codex\",\"primary\":{{\"usedPercent\":{usedPercent},\"windowDurationMins\":300,\"resetsAt\":1893456000,\"future\":1}},\"secondary\":null,\"rateLimitReachedType\":null,\"unknown\":true}}}},\"profileHome\":{profileHome},\"sqliteHome\":{sqliteHome},\"futureTopLevel\":{{}}}}}}");
                int pushedPercent = HasFakeUsageMarker("CODEX_HOME") ? usedPercent : 31;
                Console.WriteLine($"{{\"method\":\"account/rateLimits/updated\",\"params\":{{\"rateLimits\":{{\"limitId\":\"codex\",\"primary\":{{\"usedPercent\":{pushedPercent},\"windowDurationMins\":300,\"resetsAt\":1893456000}}}}}}}}");
            }
            await Console.Out.FlushAsync();
        }
        return 0;
    }

    private static async Task<int> RunFakeCodexExecutionAsync()
    {
        string home = Environment.GetEnvironmentVariable("CODEX_HOME") ?? "<null>";
        string sqlite = Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME") ?? "<null>";
        string prompt = await Console.In.ReadToEndAsync();
        string session = "codex-" + Path.GetFileName(home);
        Console.WriteLine(JsonSerializer.Serialize(new { type = "thread.started", thread_id = session }));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "item.completed",
            item = new { type = "agent_message", text = $"{home}|{sqlite}|{prompt}" }
        }));
        Console.WriteLine("{\"type\":\"turn.completed\"}");
        return 0;
    }

    private static async Task<int> RunFakeClaudeExecutionAsync()
    {
        string home = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? "<null>";
        string prompt = await Console.In.ReadToEndAsync();
        string session = "claude-" + Path.GetFileName(home);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "result",
            subtype = "success",
            is_error = false,
            result = $"{home}|{prompt}",
            session_id = session
        }));
        return 0;
    }

    private static int ReadFakeUsage(string environmentVariable)
    {
        string? directory = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(directory)) return 25;
        string marker = Path.Combine(directory, ".fake-usage-percent");
        return File.Exists(marker)
            && int.TryParse(File.ReadAllText(marker), System.Globalization.CultureInfo.InvariantCulture, out int used)
                ? used
                : 25;
    }

    private static bool HasFakeUsageMarker(string environmentVariable)
    {
        string? directory = Environment.GetEnvironmentVariable(environmentVariable);
        return !string.IsNullOrWhiteSpace(directory)
            && File.Exists(Path.Combine(directory, ".fake-usage-percent"));
    }

    private static void WriteLines(TextWriter writer, string prefix, int count, string payload)
    {
        for (var index = 0; index < count; index++)
        {
            writer.WriteLine($"{prefix}:{index:D6}:{payload}");
        }
    }

    private static Process StartChild(string markerPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(typeof(Marker).Assembly.Location);
        startInfo.ArgumentList.Add("child");
        startInfo.ArgumentList.Add(markerPath);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start child process.");
    }
}
