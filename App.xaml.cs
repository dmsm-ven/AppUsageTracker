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

        base.OnExit(e);
    }
}
