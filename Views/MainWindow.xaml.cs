using System.ComponentModel;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using MahApps.Metro.Controls;

namespace AppUsageTracker.Views;

public partial class MainWindow : MetroWindow
{
    // True only when the user has actually chosen "Exit" from the tray menu
    // (or Windows itself is shutting down). Otherwise closing the window
    // just hides it, since tracking should keep running in the background.
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
    }

    private TaskbarIcon TrayIcon => (TaskbarIcon)FindResource("TrayIcon");

    private void MetroWindow_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void MetroWindow_Closing(object sender, CancelEventArgs e)
    {
        if (_isExiting)
            return;

        // Clicking the window's X minimizes to tray instead of quitting,
        // so tracking keeps running. Use the tray menu's "Exit" to actually
        // close the app.
        e.Cancel = true;
        Hide();
    }

    private void TrayIcon_DoubleClick(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void TrayShow_Click(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        TrayIcon.Dispose();
        Close();
        Application.Current.Shutdown();
    }
}
