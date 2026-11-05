using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Murmur.Core;

namespace Murmur.Services;

public sealed class ModelManager(Store store)
{
    private readonly HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly SemaphoreSlim gate = new(1, 1);
    public List<ModelSpec> Catalog { get; } = Json.Read<List<ModelSpec>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "models.json")));
    public event Action<object>? Progress;
    private CancellationTokenSource? download;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Length, long Modified)> verified = new();
    public const string FastEnglish = "moonshine-base-en";
    public static readonly string[] FastEnglishFiles = ["moonshine-base-en/encoder_model.ort", "moonshine-base-en/decoder_model_merged.ort", "moonshine-base-en/tokens.txt"];
    public bool IsSpeechModel(string id) => id == FastEnglish || Catalog.Any(m => m.File == id && id.StartsWith("ggml-") && !id.Contains("silero"));
    public string PathFor(string file) { if (!Catalog.Any(m => m.File == file)) throw new ArgumentException("Unknown model."); return Path.Combine(store.Models, file); }
    public bool Available(string file) => file == FastEnglish ? FastEnglishFiles.All(Available) : File.Exists(PathFor(file)) && new FileInfo(PathFor(file)).Length == Catalog.Single(m => m.File == file).Bytes;
    public async Task Verify(string file, CancellationToken ct)
    {
        if (file == FastEnglish) { foreach (var part in FastEnglishFiles) await Verify(part, ct); return; }
        var path = PathFor(file); var info = new FileInfo(path); var spec = Catalog.Single(m => m.File == file);
        if (!info.Exists || info.Length != spec.Bytes) throw new InvalidOperationException("Download " + file + " using the setup buttons in Settings first.");
        var stamp = (info.Length, info.LastWriteTimeUtc.Ticks);
        if (verified.TryGetValue(file, out var known) && known == stamp) return;
        await using var input = File.OpenRead(path);
        if (!Convert.ToHexString(await SHA256.HashDataAsync(input, ct)).Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The model integrity check failed. Delete and download " + file + " again.");
        verified[file] = stamp;
    }
    public object Status()
    {
        var list = Catalog.Where(m => !FastEnglishFiles.Contains(m.File)).Select(m => new { m.File, m.Bytes, m.License, m.Repo, installed = Available(m.File), requiredDiskBytes = m.Bytes * 2, speech = IsSpeechModel(m.File), quality = m.File.Contains("turbo") ? "Whisper quality option · more memory · English" : m.File.Contains("small") ? "Whisper quality option · slower CPU · English" : m.File.Contains("base") ? "Whisper fallback · English" : m.File.Contains("tiny") ? "Whisper tiny · lower accuracy · English" : m.File.Contains("qwen") ? "Local cleanup, commands and meeting summaries" : m.File.Contains("speaker") ? "Optional speaker identification" : "Speech detection" }).ToList();
        long bytes = Catalog.Where(m => FastEnglishFiles.Contains(m.File)).Sum(m => m.Bytes);
        list.Insert(0, new { File = FastEnglish, Bytes = bytes, License = "MIT (English weights)", Repo = "Useful Sensors / Moonshine AI", installed = Available(FastEnglish), requiredDiskBytes = bytes * 2, speech = true, quality = "Recommended · fast English · smaller memory use" });
        return list;
    }
    public async Task Download(string file, CancellationToken ct = default)
    {
        if (file == FastEnglish) { foreach (var component in FastEnglishFiles) if (!Available(component)) await Download(component, ct); return; }
        if (!await gate.WaitAsync(0, ct)) throw new InvalidOperationException("A model is already downloading.");
        download = CancellationTokenSource.CreateLinkedTokenSource(ct); var token = download.Token;
        var m = Catalog.Single(x => x.File == file); var path = PathFor(file); var part = path + ".partial";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (new DriveInfo(Path.GetPathRoot(path)!).AvailableFreeSpace < m.Bytes * 2) throw new IOException("Not enough disk space. Keep twice the model size free for a safe download.");
            long offset = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (offset >= m.Bytes) { File.Delete(part); offset = 0; }
            using var request = new HttpRequestMessage(HttpMethod.Get, m.Url);
            if (offset > 0) request.Headers.Range = new(offset, null);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
            if (offset > 0 && response.StatusCode != HttpStatusCode.PartialContent) offset = 0;
            if (response.StatusCode == HttpStatusCode.PartialContent && response.Content.Headers.ContentRange?.From != offset) throw new IOException("The server returned an invalid download range.");
            await using (var output = new FileStream(part, offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            {
                byte[] buffer = new byte[65536]; int n; long total = offset; var last = DateTime.UtcNow;
                while ((n = await input.ReadAsync(buffer, token)) > 0) { total += n; if (total > m.Bytes) throw new IOException("Download exceeded the expected size."); await output.WriteAsync(buffer.AsMemory(0, n), token); if ((DateTime.UtcNow - last).TotalMilliseconds > 200) { Report(file, "downloading", total); last = DateTime.UtcNow; } }
                await output.FlushAsync(token);
            }
            Report(file, "verifying", m.Bytes);
            await using var check = File.OpenRead(part);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(check, token));
            if (check.Length != m.Bytes || !hash.Equals(m.Sha256, StringComparison.OrdinalIgnoreCase)) { check.Close(); File.Delete(part); throw new InvalidDataException("Model integrity check failed. Retry to download a fresh copy."); }
            check.Close(); File.Move(part, path, true);
            Report(file, "complete", m.Bytes);
        }
        catch (OperationCanceledException) { Report(file, "cancelled"); throw; }
        catch (Exception ex) { Report(file, "failed", error: ex.Message); throw; }
        finally { download.Dispose(); download = null; gate.Release(); }
    }
    public void Cancel() => download?.Cancel();
    private void Report(string file, string state, long received = 0, string? error = null)
    {
        long total = Catalog.Single(m => m.File == file).Bytes;
        if (FastEnglishFiles.Contains(file))
        {
            received += Catalog.Where(m => m.File != file && FastEnglishFiles.Contains(m.File) && Available(m.File)).Sum(m => m.Bytes);
            total = Catalog.Where(m => FastEnglishFiles.Contains(m.File)).Sum(m => m.Bytes);
            file = FastEnglish;
            if (state == "complete" && !Available(FastEnglish)) state = "downloading";
        }
        Progress?.Invoke(new { file, state, received, total, error });
    }
    public void Delete(string file) { if (download is not null) throw new InvalidOperationException("Finish or cancel the download first."); if (file == FastEnglish) { foreach (var part in FastEnglishFiles) Delete(part); return; } File.Delete(PathFor(file)); verified.TryRemove(file, out _); }
}
