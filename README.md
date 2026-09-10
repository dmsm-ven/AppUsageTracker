# App Usage Tracker (WPF, .NET 8, CommunityToolkit.Mvvm)

Tracks how long each application spends as the **active foreground window**,
shown both as a live session log and as totals aggregated per application.

## How it works

- **`Services/ForegroundWindowWatcher.cs`** calls the Win32 `SetWinEventHook`
  API to subscribe to `EVENT_SYSTEM_FOREGROUND`, which Windows fires every
  time the foreground window changes system-wide. This is event-driven, not
  a polling loop, so it's cheap to run continuously.
- **`Services/WindowInfoHelper.cs`** resolves a window handle (`HWND`) into
  a process name, window title, and process ID via `GetWindowThreadProcessId`
  and `GetWindowText`.
- **`Services/UsageTrackerService.cs`** is the core: every time the
  foreground window changes to a *different process*, it closes out the
  previous `AppUsageRecord` (stamping `EndTime`) and opens a new one. It
  filters out sub-second flicker (e.g. quick alt-tabs) below
  `MinimumSessionLength`. A `DispatcherTimer` ticks once a second purely to
  refresh the *live* duration of the in-progress session in the UI.
- **`Services/UsageDataStore.cs`** persists each completed session as JSON
  under `%AppData%\AppUsageTracker\history\yyyy-MM-dd.json`, one file per day.
- **`ViewModels/MainViewModel.cs`** is a `CommunityToolkit.Mvvm`
  `ObservableObject` with `[ObservableProperty]` / `[RelayCommand]` source
  generators, exposing `SessionRecords` (raw sessions) and `Summaries`
  (grouped totals per app) for binding.
- **`Views/MainWindow.xaml`** — two `DataGrid`s: session log on the left,
  totals-by-app on the right, with Start/Stop buttons.

## Running it

Requires Windows + .NET 8 SDK (this is Windows-only — `System.Windows`,
`user32.dll`, etc. don't run on Linux/macOS).

```
dotnet restore
dotnet run
```

Click **Start Tracking**, then switch between a few applications — you'll
see rows appear in the session log and totals accumulate on the right.

## Notes & things you may want to change

- **Same-window title changes** (e.g. a browser tab title changing) do
  *not* start a new session — only an actual process change does. If you
  want per-*window* (not per-process) granularity, key sessions on the
  window handle / title instead of just the process ID in
  `UsageTrackerService.OnForegroundChanged`.
- **Idle time isn't detected.** If the user walks away with an app in the
  foreground, that time still counts as usage. Add `GetLastInputInfo`
  (another Win32 call) if you want to split out idle periods.
- **Elevated (admin) windows**: if a foreground app is running as
  administrator and this app isn't, `SetWinEventHook`/`GetWindowText` can
  silently fail to see it due to UIPI. Run this app as admin too if you
  need to track elevated processes.
- **No system tray / run-at-startup yet.** For a "set it and forget it"
  tracker you'll likely want a `NotifyIcon` (via `System.Windows.Forms`
  interop or a WPF tray library) and a registry Run-key or scheduled task.
- **History view**: `UsageDataStore.LoadDay(date)` already exists for
  pulling past days back in — wire it up to a date picker if you want
  historical browsing beyond "today."
