using System.Diagnostics;
using System.Text;

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

            case "child":
                await File.WriteAllTextAsync(args[1], Environment.ProcessId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
                await Task.Delay(TimeSpan.FromMinutes(5));
                return 0;

            default:
                return 65;
        }
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
