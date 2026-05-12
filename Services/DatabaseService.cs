using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OllamaManager.Services;

public class DatabaseService : IDisposable
{
    private readonly SqliteConnection _db;

    // ~/Library/Application Support/OllamaManager  (macOS standard path)
    public static string AppSupportDir { get; } = Path.Combine(
        Environment.GetEnvironmentVariable("HOME") ??
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "OllamaManager");

    // Old incorrect path (SpecialFolder.Personal → ~/Documents on macOS)
    private static string LegacyAppSupportDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
        "Library", "Application Support", "OllamaManager");

    public DatabaseService()
    {
        Directory.CreateDirectory(AppSupportDir);
        MigrateLegacyData();
        _db = new SqliteConnection($"Data Source={Path.Combine(AppSupportDir, "settings.db")}");
        _db.Open();
        Migrate();
    }

    // Move data from the old ~/Documents/Library/... path to ~/Library/...
    private static void MigrateLegacyData()
    {
        try
        {
            if (LegacyAppSupportDir == AppSupportDir) return;
            if (!Directory.Exists(LegacyAppSupportDir)) return;

            // Migrate settings.db
            var legacyDb = Path.Combine(LegacyAppSupportDir, "settings.db");
            var newDb    = Path.Combine(AppSupportDir, "settings.db");
            if (File.Exists(legacyDb) && !File.Exists(newDb))
                File.Copy(legacyDb, newDb);

            // Migrate mlx data dir (models)
            var legacyMlx = Path.Combine(LegacyAppSupportDir, "mlx");
            var newMlx    = Path.Combine(AppSupportDir, "mlx");
            if (Directory.Exists(legacyMlx) && !Directory.Exists(newMlx))
                Directory.Move(legacyMlx, newMlx);

            // Migrate ollama models dir
            var legacyOllama = Path.Combine(LegacyAppSupportDir, "ollama");
            var newOllama    = Path.Combine(AppSupportDir, "ollama");
            if (Directory.Exists(legacyOllama) && !Directory.Exists(newOllama))
                Directory.Move(legacyOllama, newOllama);

            // Migrate open-webui data dir
            var legacyWebui = Path.Combine(LegacyAppSupportDir, "open-webui");
            var newWebui    = Path.Combine(AppSupportDir, "open-webui");
            if (Directory.Exists(legacyWebui) && !Directory.Exists(newWebui))
                Directory.Move(legacyWebui, newWebui);
        }
        catch { /* best-effort migration */ }
    }

    private void Migrate()
    {
        Exec(@"CREATE TABLE IF NOT EXISTS Settings (
                   Key   TEXT PRIMARY KEY NOT NULL,
                   Value TEXT NOT NULL
               );");
    }

    public string Get(string key, string defaultValue)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string ?? defaultValue;
    }

    public void Set(string key, string value)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ($k, $v);";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
