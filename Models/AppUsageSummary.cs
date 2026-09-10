using CommunityToolkit.Mvvm.ComponentModel;

namespace AppUsageTracker.Models;

/// <summary>
/// Aggregated total foreground time for a single process name, across
/// however many separate sessions it had.
/// </summary>
public partial class AppUsageSummary : ObservableObject
{
    public string ProcessName { get; init; } = string.Empty;

    [ObservableProperty]
    private TimeSpan totalDuration;

    [ObservableProperty]
    private int sessionCount;
}
