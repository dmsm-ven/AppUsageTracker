using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppUsageTracker.Models;

/// <summary>
/// Represents one continuous stretch of time during which a single
/// application's window was the active foreground window.
/// </summary>
public partial class AppUsageRecord : ObservableObject
{
    public string ProcessName { get; init; } = string.Empty;

    public string WindowTitle { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    [ObservableProperty]
    private DateTime? endTime;

    /// <summary>
    /// The application's small icon, resolved (and cached) by
    /// AppIconCache. Null until resolved, or if it couldn't be resolved
    /// (e.g. an elevated process this app can't query).
    /// </summary>
    [ObservableProperty]
    private ImageSource? iconSource;

    /// <summary>
    /// True while this record represents the currently active window
    /// (EndTime not yet set).
    /// </summary>
    public bool IsOngoing => EndTime is null;

    /// <summary>
    /// Live duration. For an ongoing record this is recalculated against
    /// DateTime.Now each time it's read, so the UI can refresh it on a timer tick.
    /// </summary>
    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;

    /// <summary>
    /// Call this whenever a timer tick occurs so bound UI (e.g. a DataGrid cell
    /// showing Duration) re-reads the Duration getter for ongoing records.
    /// </summary>
    public void NotifyDurationChanged() => OnPropertyChanged(nameof(Duration));

    public void Close(DateTime endTimeUtc)
    {
        EndTime = endTimeUtc;
        OnPropertyChanged(nameof(IsOngoing));
        OnPropertyChanged(nameof(Duration));
    }
}
