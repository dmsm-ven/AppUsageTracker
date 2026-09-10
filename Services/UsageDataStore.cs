using System.Globalization;
using System.IO;
using System.Text.Json;
using AppUsageTracker.Models;
using Microsoft.Data.Sqlite;

namespace AppUsageTracker.Services;

/// <summary>
/// SQLite-backed persistence for completed usage sessions. A single
/// database file under %AppData%\AppUsageTracker\usage.db holds all
/// history, so nothing is lost when the app closes or the day rolls over.
///
/// This replaces the earlier one-JSON-file-per-day storage. If that old
/// history folder is still present the first time this runs, its contents
/// are imported into the database automatically (see MigrateLegacyJsonIfNeeded).
/// </summary>
public sealed class UsageDataStore
{
    private readonly string _connectionString;

    public UsageDataStore()
    {
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AppUsageTracker");
        Directory.CreateDirectory(appFolder);

        var dbPath = Path.Combine(appFolder, "usage.db");
        _connectionString = $"Data Source={dbPath}";

        Initialize();
        MigrateLegacyJsonIfNeeded(appFolder);
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Sessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessName TEXT NOT NULL,
                WindowTitle TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Sessions_StartTime ON Sessions(StartTime);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>Persists one completed session. Call once a session's EndTime is set.</summary>
    public void AppendSession(AppUsageRecord record)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Sessions (ProcessName, WindowTitle, StartTime, EndTime)
            VALUES ($processName, $windowTitle, $startTime, $endTime);
            """;
        command.Parameters.AddWithValue("$processName", record.ProcessName);
        command.Parameters.AddWithValue("$windowTitle", record.WindowTitle);
        command.Parameters.AddWithValue("$startTime", record.StartTime.ToString("O"));
        command.Parameters.AddWithValue("$endTime", (object?)record.EndTime?.ToString("O") ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>Loads every completed session that started on the given calendar day.</summary>
    public List<AppUsageRecord> LoadDay(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ProcessName, WindowTitle, StartTime, EndTime
            FROM Sessions
            WHERE StartTime >= $dayStart AND StartTime < $dayEnd
            ORDER BY StartTime;
            """;
        command.Parameters.AddWithValue("$dayStart", dayStart.ToString("O"));
        command.Parameters.AddWithValue("$dayEnd", dayEnd.ToString("O"));

        var results = new List<AppUsageRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AppUsageRecord
            {
                ProcessName = reader.GetString(0),
                WindowTitle = reader.GetString(1),
                StartTime = ParseDateTime(reader.GetString(2)),
                EndTime = reader.IsDBNull(3) ? null : ParseDateTime(reader.GetString(3))
            });
        }
        return results;
    }

    private static DateTime ParseDateTime(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>
    /// One-time import of the old %AppData%\AppUsageTracker\history\*.json
    /// files (from before this app used SQLite) into the database, so
    /// upgrading doesn't orphan any history. Runs only if the database is
    /// still empty, and renames the old folder afterwards so it never runs
    /// twice.
    /// </summary>
    private void MigrateLegacyJsonIfNeeded(string appFolder)
    {
        var legacyFolder = Path.Combine(appFolder, "history");
        if (!Directory.Exists(legacyFolder))
            return;

        var jsonFiles = Directory.GetFiles(legacyFolder, "*.json");
        if (jsonFiles.Length == 0)
            return;

        using (var connection = OpenConnection())
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM Sessions;";
            var existingCount = (long)countCommand.ExecuteScalar()!;
            if (existingCount > 0)
                return; // Already has data — either already migrated, or started fresh on SQLite.
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<List<LegacyUsageRecordDto>>(json);
                if (entries is null)
                    continue;

                foreach (var entry in entries)
                {
                    AppendSession(new AppUsageRecord
                    {
                        ProcessName = entry.ProcessName,
                        WindowTitle = entry.WindowTitle,
                        StartTime = entry.StartTime,
                        EndTime = entry.EndTime
                    });
                }
            }
            catch
            {
                // Best-effort migration — skip any file that doesn't parse
                // rather than failing startup over old data.
            }
        }

        try
        {
            Directory.Move(legacyFolder, legacyFolder + "_migrated");
        }
        catch
        {
            // Not critical if the rename fails; the data is safely in SQLite either way.
        }
    }

    private sealed class LegacyUsageRecordDto
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
