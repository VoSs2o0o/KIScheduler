using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KIScheduler.Platforms.Codex;

public sealed class CodexAppServerClient : ICodexAppServerClient
{
    private readonly CodexOptions options;
    private readonly ILogger<CodexAppServerClient> logger;
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> pending = new();
    private readonly CancellationTokenSource lifetime = new();
    private Process? process;
    private Task? readTask;
    private Task? stderrTask;
    private long nextRequestId;
    private bool initialized;
    private bool disposed;

    public CodexAppServerClient(IOptions<CodexOptions> options, ILogger<CodexAppServerClient> logger)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler<CodexRateLimitsChangedEventArgs>? RateLimitsChanged;

    public async Task<CodexRateLimitsResponse> ReadRateLimitsAsync(CancellationToken cancellationToken = default)
    {
        JsonElement result = await SendRequestAsync("account/rateLimits/read", null, cancellationToken)
            .ConfigureAwait(false);
        return CodexRateLimitsJson.Parse(result);
    }

    private async Task<JsonElement> SendRequestAsync(string method, object? parameters,
        CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await SendRequestCoreAsync(method, parameters, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (initialized && process is { HasExited: false }) return;

        await connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized && process is { HasExited: false }) return;
            await StopProcessAsync().ConfigureAwait(false);
            StartProcess();

            _ = await SendRequestCoreAsync("initialize", new
            {
                clientInfo = new { name = "kischeduler", title = "KIScheduler", version = "1.0.0" }
            }, cancellationToken).ConfigureAwait(false);
            await SendNotificationAsync("initialized", new { }, cancellationToken).ConfigureAwait(false);
            initialized = true;
        }
        catch (CodexAppServerException)
        {
            await StopProcessAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            await StopProcessAsync().ConfigureAwait(false);
            throw new CodexAppServerException(CodexAppServerFailureKind.Connection,
                "Der Codex App Server konnte nicht gestartet oder initialisiert werden.", exception);
        }
        finally
        {
            connectionGate.Release();
        }
    }

    private void StartProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in options.AppServerArguments) startInfo.ArgumentList.Add(argument);

        process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new CodexAppServerException(CodexAppServerFailureKind.Connection,
                "Das Betriebssystem hat den Codex App Server nicht gestartet.");

        readTask = ReadLoopAsync(process, lifetime.Token);
        stderrTask = DrainStandardErrorAsync(process, lifetime.Token);
    }

    private async Task<JsonElement> SendRequestCoreAsync(string method, object? parameters,
        CancellationToken cancellationToken)
    {
        Process activeProcess = process ?? throw new CodexAppServerException(
            CodexAppServerFailureKind.Connection, "Es besteht keine Verbindung zum Codex App Server.");
        long id = Interlocked.Increment(ref nextRequestId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, completion)) throw new InvalidOperationException("Doppelte App-Server-Anfrage-ID.");

        try
        {
            var message = new Dictionary<string, object?>
            {
                ["method"] = method,
                ["id"] = id
            };
            if (parameters is not null) message["params"] = parameters;
            await WriteMessageAsync(activeProcess, message, cancellationToken)
                .ConfigureAwait(false);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            timeout.CancelAfter(options.AppServerRequestTimeout);
            try
            {
                return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested
                && !lifetime.IsCancellationRequested)
            {
                throw new CodexAppServerException(CodexAppServerFailureKind.Connection,
                    $"Der Codex App Server antwortete nicht innerhalb von {options.AppServerRequestTimeout}.", exception);
            }
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    private Task SendNotificationAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        Process activeProcess = process ?? throw new CodexAppServerException(
            CodexAppServerFailureKind.Connection, "Es besteht keine Verbindung zum Codex App Server.");
        return WriteMessageAsync(activeProcess, new { method, @params = parameters }, cancellationToken);
    }

    private async Task WriteMessageAsync(Process activeProcess, object message,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(message, CodexRateLimitsJson.Options);
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await activeProcess.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            await activeProcess.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
            throw new CodexAppServerException(CodexAppServerFailureKind.Connection,
                "Die Verbindung zum Codex App Server wurde beim Schreiben beendet.", exception);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task ReadLoopAsync(Process activeProcess, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            while (await activeProcess.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                JsonElement root;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    root = document.RootElement.Clone();
                }
                catch (JsonException exception)
                {
                    throw new CodexAppServerException(CodexAppServerFailureKind.Parse,
                        "Der Codex App Server hat ungültiges JSON gesendet.", exception);
                }

                if (root.TryGetProperty("id", out JsonElement idElement) && idElement.TryGetInt64(out long id)
                    && pending.TryGetValue(id, out TaskCompletionSource<JsonElement>? completion))
                {
                    if (root.TryGetProperty("error", out JsonElement error))
                    {
                        string message = FindMessage(error) ?? "Unbekannter App-Server-Protokollfehler.";
                        completion.TrySetException(new CodexAppServerException(ClassifyServerError(message), message));
                    }
                    else if (root.TryGetProperty("result", out JsonElement result))
                    {
                        completion.TrySetResult(result.Clone());
                    }
                    else
                    {
                        completion.TrySetException(new CodexAppServerException(CodexAppServerFailureKind.Protocol,
                            "App-Server-Antwort enthält weder result noch error."));
                    }
                    continue;
                }

                if (root.TryGetProperty("method", out JsonElement methodElement)
                    && methodElement.ValueKind == JsonValueKind.String
                    && methodElement.GetString() == "account/rateLimits/updated"
                    && root.TryGetProperty("params", out JsonElement parameters))
                {
                    PublishRateLimits(parameters);
                }
            }

            if (!cancellationToken.IsCancellationRequested)
                failure = new CodexAppServerException(CodexAppServerFailureKind.Connection,
                    "Der Codex App Server hat die Verbindung beendet.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (failure is not null) FailPending(failure);
        }
    }

    private void PublishRateLimits(JsonElement parameters)
    {
        try
        {
            CodexRateLimitsResponse response = CodexRateLimitsJson.Parse(parameters);
            RateLimitsChanged?.Invoke(this, new CodexRateLimitsChangedEventArgs(response));
        }
        catch (Exception exception)
        {
            logger.LogWarning("Eine Rate-Limit-Aktualisierung des Codex App Servers konnte nicht verarbeitet werden: {Type}",
                exception.GetType().Name);
        }
    }

    private async Task DrainStandardErrorAsync(Process activeProcess, CancellationToken cancellationToken)
    {
        try
        {
            while (await activeProcess.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is not null)
                logger.LogDebug("Codex App Server hat eine Diagnosezeile auf stderr ausgegeben.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static CodexAppServerFailureKind ClassifyServerError(string message) =>
        CodexJsonlParser.ContainsAuthenticationText(message)
            ? CodexAppServerFailureKind.Authentication
            : CodexAppServerFailureKind.Protocol;

    private static string? FindMessage(JsonElement error) => error.ValueKind == JsonValueKind.Object
        && error.TryGetProperty("message", out JsonElement message)
        && message.ValueKind == JsonValueKind.String ? message.GetString() : null;

    private void FailPending(Exception exception)
    {
        initialized = false;
        foreach (TaskCompletionSource<JsonElement> completion in pending.Values)
            completion.TrySetException(exception);
    }

    private async Task StopProcessAsync()
    {
        initialized = false;
        Process? oldProcess = process;
        process = null;
        if (oldProcess is null) return;
        try
        {
            oldProcess.StandardInput.Close();
            if (!oldProcess.HasExited) oldProcess.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            logger.LogDebug("Codex App Server war beim Beenden bereits nicht mehr verfügbar.");
        }
        if (readTask is not null)
        {
            try { await readTask.ConfigureAwait(false); }
            catch { }
        }
        if (stderrTask is not null)
        {
            try { await stderrTask.ConfigureAwait(false); }
            catch { }
        }
        oldProcess.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        await lifetime.CancelAsync().ConfigureAwait(false);
        FailPending(new ObjectDisposedException(nameof(CodexAppServerClient)));
        await StopProcessAsync().ConfigureAwait(false);
        lifetime.Dispose();
        connectionGate.Dispose();
        writeGate.Dispose();
    }
}
