using AppUsageTracker.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppUsageTracker.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool openOnSystemStart;

    public SettingsViewModel()
    {
        // Read the real current state from the registry rather than some
        // separately-stored setting, so this checkbox can never drift out
        // of sync with what Windows will actually do at logon.
        openOnSystemStart = StartupManager.IsEnabled;
    }

    partial void OnOpenOnSystemStartChanged(bool value) => StartupManager.SetEnabled(value);
}
