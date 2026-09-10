using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AppUsageTracker.Converters;

/// <summary>
/// Green dot while tracking is active, gray dot while stopped. Used next to
/// the status text in the toolbar.
/// </summary>
public class BoolToTrackingBrushConverter : IValueConverter
{
    private static readonly Brush TrackingBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xA0, 0x4A));
    private static readonly Brush StoppedBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrackingBrush : StoppedBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
