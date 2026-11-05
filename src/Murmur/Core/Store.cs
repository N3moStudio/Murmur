using Microsoft.Data.Sqlite;

namespace Murmur.Core;

public sealed class Store : IDisposable
{
    public string Root { get; }
    public string Recordings => Path.Combine(Root, "recordings");
    public string Models => Path.Combine(Root, "models");
    public string Database => Path.Combine(Root, "murmur.db");
    public string InstanceKey => "Local\\N3moStudio.Murmur." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())))[..12];
    private readonly object gate = new();
    private readonly SqliteConnection connection;
    public event Action? Changed;
    public Store(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "N3moStudio", Brand.Name);
        Directory.CreateDirectory(Root); Directory.CreateDirectory(Recordings); Directory.CreateDirectory(Models);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database }.ToString());
        connection.Open();
        using var version = connection.CreateCommand(); version.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(version.ExecuteScalar()) > 1) throw new InvalidDataException("This library was created by a newer Murmur. Please update before opening it.");
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS documents(id TEXT NOT NULL,kind TEXT NOT NULL,json TEXT NOT NULL,updated TEXT NOT NULL,deleted INTEGER NOT NULL DEFAULT 0,PRIMARY KEY(id,kind)); CREATE INDEX IF NOT EXISTS documents_kind ON documents(kind,deleted,updated); PRAGMA user_version=1;";
        cmd.ExecuteNonQuery();
    }
    public T? Get<T>(string kind, string id)
    {
        lock (gate) { using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT json FROM documents WHERE kind=$kind AND id=$id AND deleted=0"; cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$id", id); var json = cmd.ExecuteScalar() as string; return json is null ? default : Json.Read<T>(json); }
    }
    public List<T> List<T>(string kind, int limit = 10000)
    {
        lock (gate) { using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT json FROM documents WHERE kind=$kind AND deleted=0 ORDER BY updated DESC LIMIT $limit"; cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 10000)); using var reader = cmd.ExecuteReader(); var result = new List<T>(); while (reader.Read()) result.Add(Json.Read<T>(reader.GetString(0))); return result; }
    }
    public void Save<T>(string kind, string id, T item) => SaveBatch([(kind, id, Json.Write(item))]);
    public void SaveBatch(IEnumerable<(string Kind, string Id, string Json)> items)
    {
        lock (gate)
        {
            using var tx = connection.BeginTransaction();
            foreach (var (kind, id, json) in items) { using var cmd = connection.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "INSERT INTO documents(id,kind,json,updated,deleted) VALUES($id,$kind,$json,$updated,0) ON CONFLICT(id,kind) DO UPDATE SET json=excluded.json,updated=excluded.updated,deleted=0"; cmd.Parameters.AddWithValue("$id", id); cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$json", json); cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O")); cmd.ExecuteNonQuery(); }
            tx.Commit();
        }
        Changed?.Invoke();
    }
    public void Delete(string kind, string id)
    {
        lock (gate) { using var cmd = connection.CreateCommand(); cmd.CommandText = "UPDATE documents SET deleted=1,json='{}',updated=$updated WHERE kind=$kind AND id=$id"; cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$id", id); cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O")); cmd.ExecuteNonQuery(); }
        Changed?.Invoke();
    }
    public Settings Settings { get => Get<Settings>("settings", "main") ?? new(); set => Save("settings", "main", value); }
    public List<StoredDocument> Documents(params string[] kinds)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand(); command.CommandText = "SELECT kind,id,json,updated,deleted FROM documents";
            using var reader = command.ExecuteReader(); var result = new List<StoredDocument>();
            while (reader.Read()) if (kinds.Contains(reader.GetString(0))) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), DateTimeOffset.Parse(reader.GetString(3)), reader.GetBoolean(4)));
            return result;
        }
    }
    public void ApplyDocuments(IEnumerable<StoredDocument> documents)
    {
        lock (gate)
        {
            using var tx = connection.BeginTransaction();
            foreach (var d in documents)
            {
                using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO documents(id,kind,json,updated,deleted) VALUES($id,$kind,$json,$updated,$deleted) ON CONFLICT(id,kind) DO UPDATE SET json=excluded.json,updated=excluded.updated,deleted=excluded.deleted";
                cmd.Parameters.AddWithValue("$id", d.Id); cmd.Parameters.AddWithValue("$kind", d.Kind); cmd.Parameters.AddWithValue("$json", d.Deleted ? "{}" : d.Json); cmd.Parameters.AddWithValue("$updated", d.Updated.ToUniversalTime().ToString("O")); cmd.Parameters.AddWithValue("$deleted", d.Deleted ? 1 : 0); cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        Changed?.Invoke();
    }
    public void Backup(string destination)
    {
        lock (gate) { using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination }.ToString()); target.Open(); connection.BackupDatabase(target); }
    }
    public bool IsRecordingPath(string path) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(Recordings) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public void DeleteRecording(string? path) { if (path is not null && IsRecordingPath(path) && File.Exists(path)) File.Delete(path); }
    public void Prune()
    {
        var settings = Settings;
        foreach (var item in List<HistoryItem>("history"))
        {
            if (item.Status != "inserted") continue; // Keep failed/cancelled/uninserted work recoverable.
            if (!settings.RetainHistory || (settings.RetentionDays > 0 && item.Created < DateTimeOffset.UtcNow.AddDays(-settings.RetentionDays))) { DeleteRecording(item.Audio); Delete("history", item.Id); }
            else if (!settings.RetainAudio && item.Audio is not null) { DeleteRecording(item.Audio); Save("history", item.Id, item with { Audio = null }); }
        }
    }
    public void Dispose() { lock (gate) connection.Dispose(); }
}
