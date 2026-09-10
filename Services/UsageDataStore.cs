using System.IO;
using System.Text.Json;
using AppUsageTracker.Models;

namespace AppUsageTracker.Services;

/// <summary>
/// Simple JSON-file persistence for completed usage sessions, stored per
/// day under %AppData%\AppUsageTracker\history\.
/// </summary>
public sealed class UsageDataStore
{
    private readonly string _folder;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public UsageDataStore()
    {
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AppUsageTracker", "history");
        Directory.CreateDirectory(_folder);
    }

    private string FileForDate(DateTime date) =>
        Path.Combine(_folder, $"{date:yyyy-MM-dd}.json");

    public void AppendSession(AppUsageRecord record)
    {
        var path = FileForDate(record.StartTime.Date);
        var existing = LoadFile(path);
        existing.Add(ToDto(record));
        File.WriteAllText(path, JsonSerializer.Serialize(existing, _jsonOptions));
    }

    public List<AppUsageRecord> LoadDay(DateTime date)
    {
        var path = FileForDate(date.Date);
        return LoadFile(path).Select(FromDto).ToList();
    }

    private List<UsageRecordDto> LoadFile(string path)
    {
        if (!File.Exists(path))
            return new List<UsageRecordDto>();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<UsageRecordDto>>(json) ?? new List<UsageRecordDto>();
    }

    private static UsageRecordDto ToDto(AppUsageRecord r) => new()
    {
        ProcessName = r.ProcessName,
        WindowTitle = r.WindowTitle,
        StartTime = r.StartTime,
        EndTime = r.EndTime
    };

    private static AppUsageRecord FromDto(UsageRecordDto d) => new()
    {
        ProcessName = d.ProcessName,
        WindowTitle = d.WindowTitle,
        StartTime = d.StartTime,
        EndTime = d.EndTime
    };

    private class UsageRecordDto
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
