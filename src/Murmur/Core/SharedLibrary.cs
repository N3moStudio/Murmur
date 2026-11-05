using System.Security.Cryptography;
using System.Text;

namespace Murmur.Core;

public sealed record SharedEnvelope(int Version, List<StoredDocument> Documents);
public sealed record SharedMembership(List<string> Keys, List<StoredDocument>? Published = null);

// An explicit shared-folder adapter. SMB ACLs or the user's sync provider own
// access control. Only selected entries/folders enter this exchange.
public sealed class SharedLibrary(Store store)
{
    private static string Key(StoredDocument d) => d.Kind + ":" + d.Id;
    public int Sync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Choose a shared folder first.");
        folder = Path.GetFullPath(folder); Directory.CreateDirectory(folder);
        string membershipId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder.ToUpperInvariant())));
        var previousSync = store.Get<SharedMembership>("sharing", membershipId);
        var membership = previousSync?.Keys.ToHashSet() ?? [];
        var previousPublic = previousSync?.Published?.ToDictionary(Key) ?? [];
        string path = Path.Combine(folder, "murmur-library-v2.json");
        using var exclusive = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path) && new FileInfo(path).Length > 10_000_000) throw new InvalidDataException("The shared library exceeds 10 MB.");
        var remote = File.Exists(path) ? Json.Read<SharedEnvelope>(File.ReadAllText(path)) : new(2, []);
        if (remote.Version != 2 || remote.Documents.Count > 10000) throw new InvalidDataException("Unsupported or oversized shared library.");
        foreach (var d in remote.Documents) Validate(d);
        var local = store.Documents("dictionary", "snippet", "folder", "meeting");
        var sharedFolders = local.Where(d => d.Kind == "folder" && !d.Deleted && Json.Read<Folder>(d.Json).Shared).Select(d => d.Id).ToHashSet();
        bool Selected(StoredDocument d) => !d.Deleted && d.Kind switch
        {
            "dictionary" or "snippet" => Json.Read<LibraryEntry>(d.Json).Shared,
            "folder" => Json.Read<Folder>(d.Json).Shared,
            "meeting" => Json.Read<Meeting>(d.Json).FolderId is { } id && sharedFolders.Contains(id),
            _ => false
        };
        var outbound = new List<StoredDocument>();
        var privateKeys = local.Where(d => !d.Deleted && !Selected(d)).Select(Key).ToHashSet();
        foreach (var d in local)
        {
            if (Selected(d))
            {
                var publish = d.Kind == "meeting" ? d with { Json = Json.Write(Json.Read<Meeting>(d.Json) with { Notes = "", Audio = null }) } : d;
                // Editing private notes/audio must not win a public-data conflict.
                if (previousPublic.TryGetValue(Key(d), out var previous) && !previous.Deleted && previous.Json == publish.Json) publish = publish with { Updated = previous.Updated };
                outbound.Add(publish);
            }
            else if (membership.Contains(Key(d))) outbound.Add(d with { Json = "{}", Deleted = true });
        }
        var merged = remote.Documents.Concat(outbound).GroupBy(Key).Select(group => group.OrderByDescending(d => d.Updated).ThenByDescending(d => d.Deleted).ThenByDescending(d => d.Json, StringComparer.Ordinal).First()).ToList();
        if (merged.Count > 10000) throw new InvalidDataException("The shared library contains too many items.");
        foreach (var d in merged) Validate(d);
        var publishedFolders = merged.Where(d => d.Kind == "folder" && !d.Deleted).Select(d => d.Id).ToHashSet();
        if (merged.Any(d => d.Kind == "meeting" && !d.Deleted && !publishedFolders.Contains(Json.Read<Meeting>(d.Json).FolderId!))) throw new InvalidDataException("A shared meeting has no shared parent folder.");
        string serialized = Json.Write(new SharedEnvelope(2, merged));
        if (Encoding.UTF8.GetByteCount(serialized) > 10_000_000) throw new InvalidDataException("The shared library exceeds 10 MB.");
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(staging, serialized); File.Move(staging, path, true); }
        finally { File.Delete(staging); }
        var localByKey = local.ToDictionary(Key);
        var apply = new List<StoredDocument>();
        foreach (var document in merged)
        {
            if (privateKeys.Contains(Key(document))) continue;
            var d = document;
            if (!d.Deleted && d.Kind == "meeting" && localByKey.TryGetValue(Key(d), out var prior) && !prior.Deleted)
            {
                var personal = Json.Read<Meeting>(prior.Json);
                d = d with { Json = Json.Write(Json.Read<Meeting>(d.Json) with { Notes = personal.Notes, Audio = personal.Audio }) };
            }
            apply.Add(d);
        }
        store.ApplyDocuments(apply);
        store.Save("sharing", membershipId, new SharedMembership(merged.Select(Key).ToList(), merged));
        return merged.Count(d => !d.Deleted);
    }
    private static void Validate(StoredDocument d)
    {
        if (!Guid.TryParse(d.Id, out _) || d.Kind is not ("dictionary" or "snippet" or "folder" or "meeting") || d.Updated > DateTimeOffset.UtcNow.AddMinutes(5)) throw new InvalidDataException("The shared library has an invalid identifier or timestamp.");
        if (d.Deleted) return;
        if (d.Json.Length > 2_000_000) throw new InvalidDataException("A shared item is too large.");
        if (d.Kind is "dictionary" or "snippet")
        {
            var entry = Json.Read<LibraryEntry>(d.Json);
            if (entry.Id != d.Id || entry.Kind != d.Kind || !entry.Shared || TextRules.Validate(entry) is { Length: > 0 }) throw new InvalidDataException("Invalid shared vocabulary or snippet.");
        }
        else if (d.Kind == "folder")
        {
            var f = Json.Read<Folder>(d.Json);
            if (f.Id != d.Id || !f.Shared || string.IsNullOrWhiteSpace(f.Name) || f.Name.Length > 200 || f.Description.Length > 2000) throw new InvalidDataException("Invalid shared folder.");
        }
        else
        {
            var m = Json.Read<Meeting>(d.Json);
            if (m.Id != d.Id || m.Notes.Length != 0 || m.Audio is { Count: > 0 } || m.Title.Length > 200 || !Guid.TryParse(m.FolderId, out _)) throw new InvalidDataException("Shared meetings must exclude private notes and audio.");
        }
    }
}
