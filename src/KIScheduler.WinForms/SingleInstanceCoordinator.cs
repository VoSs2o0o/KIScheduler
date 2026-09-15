using System.Runtime.InteropServices;

namespace KIScheduler.WinForms;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string InstanceName = @"Local\KIScheduler.Application";
    private const string ActivationEventName = @"Local\KIScheduler.Application.Activate";
    private const uint AllowAnyProcess = uint.MaxValue;

    private readonly EventWaitHandle activationEvent;
    private readonly EventWaitHandle shutdownEvent = new(false, EventResetMode.ManualReset);
    private readonly Mutex instanceMutex;
    private Thread? activationThread;
    private bool disposed;

    private SingleInstanceCoordinator(
        EventWaitHandle activationEvent,
        Mutex instanceMutex,
        bool isPrimaryInstance)
    {
        this.activationEvent = activationEvent;
        this.instanceMutex = instanceMutex;
        IsPrimaryInstance = isPrimaryInstance;
    }

    public bool IsPrimaryInstance { get; }

    public static SingleInstanceCoordinator Acquire()
    {
        var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        var instanceMutex = new Mutex(false, InstanceName, out bool isPrimaryInstance);
        return new SingleInstanceCoordinator(activationEvent, instanceMutex, isPrimaryInstance);
    }

    public void NotifyPrimaryInstance()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsPrimaryInstance) return;

        // The newly launched process normally owns the foreground permission. Hand it to the
        // existing UI process before asking that process to restore and activate its window.
        _ = AllowSetForegroundWindow(AllowAnyProcess);
        activationEvent.Set();
    }

    public void StartListening(Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!IsPrimaryInstance || activationThread is not null) return;

        activationThread = new Thread(() => ListenForActivation(activate))
        {
            IsBackground = true,
            Name = "KIScheduler single-instance activation"
        };
        activationThread.Start();
    }

    private void ListenForActivation(Action activate)
    {
        WaitHandle[] handles = [activationEvent, shutdownEvent];
        while (WaitHandle.WaitAny(handles) == 0)
        {
            try
            {
                activate();
            }
            catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
            {
                // The form can be disposed between receiving the signal and dispatching to its UI thread.
                return;
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        shutdownEvent.Set();
        activationThread?.Join(TimeSpan.FromSeconds(2));
        shutdownEvent.Dispose();
        activationEvent.Dispose();
        instanceMutex.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
