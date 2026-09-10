using System.Diagnostics;
using System.Text;

namespace AppUsageTracker.Services;

internal static class WindowInfoHelper
{
    public static IntPtr GetCurrentForegroundWindow() => NativeMethods.GetForegroundWindow();

    /// <summary>
    /// Resolves a window handle to (ProcessName, WindowTitle, ProcessId).
    /// Returns null if the window/process can no longer be queried
    /// (e.g. it closed between the event firing and this call running).
    /// </summary>
    public static (string ProcessName, string WindowTitle, int ProcessId)? GetWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out int pid);
        if (pid == 0)
            return null;

        string title = GetWindowTitle(hwnd);

        string processName;
        try
        {
            using var proc = Process.GetProcessById(pid);
            processName = proc.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process already exited.
            return null;
        }

        return (processName, title, pid);
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length == 0)
            return string.Empty;

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
