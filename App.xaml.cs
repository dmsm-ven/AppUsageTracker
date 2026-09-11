using System.Windows;
using AppUsageTracker.ViewModels;

namespace AppUsageTracker;

public partial class App : Application
{
    protected override void OnExit(ExitEventArgs e)
    {
        // Give the active window's ViewModel a chance to flush the current
        // session and persist data before the process actually terminates.
        if (MainWindow?.DataContext is MainViewModel vm)
        {
            vm.Shutdown();
        }

        // Belt-and-braces: make sure the tray icon is removed even if exit
        // happened via a path other than the tray context menu's "Exit"
        // (e.g. Task Manager, Windows sign-out).
        if (MainWindow is Views.MainWindow mainWindow)
        {
            mainWindow.DisposeTrayIcon();
        }

        base.OnExit(e);
    }
}
