using System.Diagnostics;
using System.Text;

namespace AppUsageTracker.Services;

internal static class WindowInfoHelper
{
    public static IntPtr GetCurrentForegroundWindow() => NativeMethods.GetForegroundWindow();

    /// <summary>
    /// Resolves a window handle to (ProcessName, WindowTitle, ProcessId, ExePath).
    /// Returns null if the window/process can no longer be queried
    /// (e.g. it closed between the event firing and this call running).
    /// ExePath is null when it can't be resolved (e.g. an elevated process
    /// this app can't query without matching privileges) — icon lookup
    /// just falls back to no icon in that case.
    /// </summary>
    public static (string ProcessName, string WindowTitle, int ProcessId, string? ExePath)? GetWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out int pid);
        if (pid == 0)
            return null;

        string title = GetWindowTitle(hwnd);

        string processName;
        string? exePath;
        try
        {
            using var proc = Process.GetProcessById(pid);
            processName = proc.ProcessName;

            try
            {
                exePath = proc.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // Access denied (e.g. elevated/system process) or the
                // process exited between the two calls — no icon, but we
                // can still track the session by name.
                exePath = null;
            }
        }
        catch (ArgumentException)
        {
            // Process already exited.
            return null;
        }

        return (processName, title, pid, exePath);
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
