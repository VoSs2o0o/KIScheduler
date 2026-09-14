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
                Console.WriteLine($"{{\"id\":{id.GetRawText()},\"result\":{{\"rateLimitsByLimitId\":{{\"codex\":{{\"limitId\":\"codex\",\"limitName\":\"Codex\",\"primary\":{{\"usedPercent\":25,\"windowDurationMins\":300,\"resetsAt\":1893456000,\"future\":1}},\"secondary\":null,\"rateLimitReachedType\":null,\"unknown\":true}}}},\"profileHome\":{profileHome},\"sqliteHome\":{sqliteHome},\"futureTopLevel\":{{}}}}}}");
                Console.WriteLine("{\"method\":\"account/rateLimits/updated\",\"params\":{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"usedPercent\":31,\"windowDurationMins\":300,\"resetsAt\":1893456000}}}}");
            }
            await Console.Out.FlushAsync();
        }
        return 0;
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
