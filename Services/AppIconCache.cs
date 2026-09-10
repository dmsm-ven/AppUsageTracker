using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppUsageTracker.Services;

/// <summary>
/// Resolves the small taskbar-style icon for a tracked application and
/// caches it so the (relatively expensive) Win32 icon extraction only ever
/// happens once per application — both in memory for the lifetime of the
/// process, and as a PNG on disk under
/// %AppData%\AppUsageTracker\icons\ so it survives across app restarts too.
/// </summary>
internal static class AppIconCache
{
    private static readonly string CacheFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AppUsageTracker", "icons");

    // In-memory cache keyed by process name (e.g. "chrome"), so repeated
    // sessions of the same app within a run never touch disk or the icon
    // extraction API more than once.
    private static readonly ConcurrentDictionary<string, ImageSource?> MemoryCache = new();

    static AppIconCache()
    {
        Directory.CreateDirectory(CacheFolder);
    }

    /// <summary>
    /// Fast, synchronous, no I/O — returns whatever is already in memory
    /// for this process name, or null if nothing has been resolved for it
    /// yet in this run.
    /// </summary>
    public static ImageSource? TryGetCached(string processName)
        => MemoryCache.TryGetValue(processName, out var icon) ? icon : null;

    /// <summary>
    /// Resolves the icon for a process, checking the in-memory cache, then
    /// the on-disk cache, and only falling back to extracting it from the
    /// exe (and writing it to disk for next time) if neither has it yet.
    /// Returns null if no icon could be resolved (e.g. exePath is
    /// unavailable, or the file has no associated icon).
    /// </summary>
    public static ImageSource? GetOrLoad(string processName, string? exePath)
    {
        if (MemoryCache.TryGetValue(processName, out var cached))
            return cached;

        var diskPath = DiskPathFor(processName);
        ImageSource? icon = null;

        try
        {
            if (File.Exists(diskPath))
            {
                icon = LoadFromDisk(diskPath);
            }
            else if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                icon = ExtractAndCache(exePath, diskPath);
            }
        }
        catch
        {
            // Icon extraction/loading is a nice-to-have; never let a bad
            // icon file or a locked/odd exe take down tracking.
            icon = null;
        }

        MemoryCache[processName] = icon;
        return icon;
    }

    private static string DiskPathFor(string processName)
    {
        var safeName = string.Join("_", processName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(CacheFolder, safeName + ".png");
    }

    private static ImageSource? LoadFromDisk(string diskPath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(diskPath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageSource? ExtractAndCache(string exePath, string diskPath)
    {
        using var icon = Icon.ExtractAssociatedIcon(exePath);
        if (icon is null)
            return null;

        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        bitmapSource.Freeze();

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using var stream = File.Create(diskPath);
            encoder.Save(stream);
        }
        catch
        {
            // If writing the cache file fails (e.g. permissions), we still
            // have the icon in memory for this run — just skip persistence.
        }

        return bitmapSource;
    }
}
