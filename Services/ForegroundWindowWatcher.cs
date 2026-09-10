namespace AppUsageTracker.Services;

/// <summary>
/// Wraps SetWinEventHook to notify a listener every time the foreground
/// window changes, system-wide. This is event-driven rather than polling,
/// so it's cheap to leave running. It relies on the calling thread pumping
/// Windows messages — the WPF UI thread already does this, so call
/// Start()/Stop() from the UI thread.
/// </summary>
public sealed class ForegroundWindowWatcher : IDisposable
{
    // Keep a reference to the delegate for the lifetime of the hook so the
    // GC doesn't collect it out from under the unmanaged callback.
    private readonly NativeMethods.WinEventDelegate _callback;
    private IntPtr _hookHandle = IntPtr.Zero;

    public event Action<IntPtr>? ForegroundChanged;

    public ForegroundWindowWatcher()
    {
        _callback = OnWinEvent;
    }

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning)
            return;

        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _callback,
            idProcess: 0,
            idThread: 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        NativeMethods.UnhookWinEvent(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // idObject != 0 filters out some noisy non-window-level events.
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero)
        {
            ForegroundChanged?.Invoke(hwnd);
        }
    }

    public void Dispose() => Stop();
}
