using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppUsageTracker.Models;
using AppUsageTracker.Services;

namespace AppUsageTracker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly UsageTrackerService _tracker;
    private readonly UsageDataStore _dataStore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(StartTrackingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopTrackingCommand))]
    private bool isTracking;

    public string StatusText => IsTracking ? "Tracking active window..." : "Tracking stopped";

    /// <summary>Live, per-session records for today (bind a DataGrid to this).</summary>
    public ObservableCollection<AppUsageRecord> SessionRecords => _tracker.Records;

    /// <summary>Aggregated total time per application (bind a second DataGrid/list to this).</summary>
    public ObservableCollection<AppUsageSummary> Summaries { get; } = new();

    public MainViewModel() : this(new UsageTrackerService(), new UsageDataStore())
    {
    }

    // Constructor allows injecting fakes for testing if desired.
    public MainViewModel(UsageTrackerService tracker, UsageDataStore dataStore)
    {
        _tracker = tracker;
        _dataStore = dataStore;

        _tracker.Ticked += RefreshSummaries;
        _tracker.SessionCompleted += OnSessionCompleted;
        _tracker.Records.CollectionChanged += (_, _) => RefreshSummaries();

        LoadTodaysHistory();
    }

    /// <summary>
    /// Restores today's already-completed sessions from the database into
    /// the UI on startup, so closing and reopening the app doesn't make it
    /// look like the day's history disappeared.
    /// </summary>
    private void LoadTodaysHistory()
    {
        foreach (var record in _dataStore.LoadDay(DateTime.Now))
        {
            record.IconSource = AppIconCache.TryGetCached(record.ProcessName);
            _tracker.Records.Add(record);

            if (record.IconSource is null)
            {
                var target = record;
                Task.Run(() => AppIconCache.GetOrLoad(record.ProcessName, exePath: null))
                    .ContinueWith(t =>
                    {
                        if (t.Result is { } icon)
                            target.IconSource = icon;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        RefreshSummaries();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartTracking()
    {
        _tracker.Start();
        IsTracking = true;
    }

    private bool CanStart() => !IsTracking;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void StopTracking()
    {
        _tracker.Stop();
        IsTracking = false;
        RefreshSummaries();
    }

    private bool CanStop() => IsTracking;

    private void OnSessionCompleted(AppUsageRecord record)
    {
        _dataStore.AppendSession(record);
    }

    private void RefreshSummaries()
    {
        var grouped = SessionRecords
            .GroupBy(r => r.ProcessName)
            .Select(g => new
            {
                g.Key,
                Total = g.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.Duration),
                Count = g.Count(),
                // Every record for this process shares the same cached
                // icon once resolved; grab it from whichever one has it.
                Icon = g.Select(r => r.IconSource).FirstOrDefault(icon => icon is not null)
            })
            .OrderByDescending(g => g.Total);

        Summaries.Clear();
        foreach (var g in grouped)
        {
            Summaries.Add(new AppUsageSummary
            {
                ProcessName = g.Key,
                TotalDuration = g.Total,
                SessionCount = g.Count,
                IconSource = g.Icon
            });
        }
    }

    /// <summary>Called from App.OnExit to make sure the in-flight session gets persisted.</summary>
    public void Shutdown()
    {
        if (IsTracking)
            StopTracking();
    }
}
