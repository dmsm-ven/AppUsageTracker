using System.Collections.ObjectModel;
using System.Windows.Threading;
using AppUsageTracker.Models;

namespace AppUsageTracker.Services;

/// <summary>
/// Owns the foreground-window watcher and turns raw HWND change events into
/// AppUsageRecord sessions. Exposes an ObservableCollection the UI can bind
/// to directly.
/// </summary>
public sealed class UsageTrackerService : IDisposable
{
    private readonly ForegroundWindowWatcher _watcher = new();
    private readonly DispatcherTimer _tickTimer;

    // Sessions shorter than this are treated as noise (alt-tab flicks,
    // window manager churn) and discarded instead of being recorded.
    private static readonly TimeSpan MinimumSessionLength = TimeSpan.FromSeconds(1);

    private AppUsageRecord? _current;
    private int _lastProcessId = -1;

    public ObservableCollection<AppUsageRecord> Records { get; } = new();

    /// <summary>Raised once a second while tracking, so the UI can refresh live durations.</summary>
    public event Action? Ticked;

    /// <summary>Raised whenever a session is closed out and finalized (useful for persistence).</summary>
    public event Action<AppUsageRecord>? SessionCompleted;

    public bool IsTracking { get; private set; }

    public UsageTrackerService()
    {
        _watcher.ForegroundChanged += OnForegroundChanged;

        _tickTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += (_, _) =>
        {
            _current?.NotifyDurationChanged();
            Ticked?.Invoke();
        };
    }

    public void Start()
    {
        if (IsTracking)
            return;

        IsTracking = true;
        _watcher.Start();
        _tickTimer.Start();

        // Capture whatever window is already active right now, rather than
        // waiting for the next change event.
        var hwnd = WindowInfoHelper.GetCurrentForegroundWindow();
        if (hwnd != IntPtr.Zero)
            OnForegroundChanged(hwnd);
    }

    public void Stop()
    {
        if (!IsTracking)
            return;

        IsTracking = false;
        _watcher.Stop();
        _tickTimer.Stop();
        CloseCurrentSession();
    }

    private void OnForegroundChanged(IntPtr hwnd)
    {
        var info = WindowInfoHelper.GetWindowInfo(hwnd);
        if (info is null)
            return;

        var (processName, windowTitle, processId) = info.Value;

        // Some apps (browsers, IDEs) fire foreground events on title-only
        // changes within the same process/window. We only want a new
        // session when the process actually changes.
        if (processId == _lastProcessId && _current is not null)
            return;

        CloseCurrentSession();

        _current = new AppUsageRecord
        {
            ProcessName = processName,
            WindowTitle = windowTitle,
            StartTime = DateTime.Now
        };
        _lastProcessId = processId;
        Records.Add(_current);
    }

    private void CloseCurrentSession()
    {
        if (_current is null)
            return;

        _current.Close(DateTime.Now);

        if (_current.Duration < MinimumSessionLength)
        {
            Records.Remove(_current);
        }
        else
        {
            SessionCompleted?.Invoke(_current);
        }

        _current = null;
        _lastProcessId = -1;
    }

    public void Dispose()
    {
        Stop();
        _watcher.Dispose();
    }
}
