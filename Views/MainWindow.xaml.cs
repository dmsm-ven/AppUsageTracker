using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;

namespace AppUsageTracker.Views;

public partial class MainWindow : Window
{
    // True only when the user has actually chosen "Exit" from the tray menu
    // (or Windows itself is shutting down). Otherwise closing the window
    // just hides it, since tracking should keep running in the background.
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();

        // TrayIcon is declared as a resource, not placed in the visual
        // tree, so its Loaded event never fires and the native icon would
        // otherwise never actually get created. ForceCreate() makes sure
        // it shows up immediately instead of silently never appearing.
        TrayIcon.ForceCreate();
    }

    private TaskbarIcon TrayIcon => (TaskbarIcon)FindResource("TrayIcon");

    private void MainWindow_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
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

    private void TrayIcon_LeftClick(object sender, RoutedEventArgs e) => RestoreFromTray();

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

    // Fallback so the window can always be dragged by its toolbar, in case
    // the native MetroWindow title-bar chrome isn't draggable in your
    // environment (e.g. a MahApps/IconPacks version mismatch). Only fires
    // when the click originates from empty toolbar space, not from a
    // button, so Start/Stop still work normally.
    private void Toolbar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender && e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // DragMove throws if called outside a mouse-down event or
                // while the mouse button is no longer pressed — safe to ignore.
            }
        }
    }
}
