using System.Diagnostics;
using System.IO;
using Murmur.Core;
using Murmur.Services;

// File-only benchmark: no microphone, windows, clipboard or key injection.
using var store = new Store(args[0]);
var models = new ModelManager(store);
using var speech = new SpeechService(store, models);
using var text = new TextService(store, models);
if (args.Contains("--limit-check"))
{
    bool rejected = false;
    try { await text.Generate("Repeat the supplied text exactly, with no omissions.", "Keep the entire final sentence and every word before it. Do not discard the ending of this long message.", CancellationToken.None, 1); }
    catch (InvalidOperationException e) when (e.Message.Contains("stopped before completing")) { rejected = true; }
    Console.WriteLine($"Truncated writing response rejected: {rejected}");
    Environment.ExitCode = rejected ? 0 : 1; return;
}
await speech.Prepare(CancellationToken.None); await text.Prepare(CancellationToken.None);
var preparation = new List<object>();
foreach (string prefix in Directory.GetFiles(args[1], "prefix-*.wav").OrderBy(p => int.Parse(Path.GetFileNameWithoutExtension(p)[7..])))
{
    var recognition = await speech.Transcribe(prefix, "Fixture", CancellationToken.None);
    string raw = string.Join(" ", recognition.Segments.Select(s => s.Text));
    var cleanup = await text.Cleanup(raw, "fixture", CancellationToken.None, "email");
    preparation.Add(new { prefix = Path.GetFileName(prefix), speechMs = recognition.Milliseconds, cleanupMs = cleanup.Milliseconds, warning = cleanup.Warning });
}
var watch = Stopwatch.StartNew();
var final = await speech.Transcribe(Path.Combine(args[1], "full.wav"), "Fixture", CancellationToken.None);
string finalRaw = string.Join(" ", final.Segments.Select(s => s.Text));
var result = await text.Cleanup(finalRaw, "fixture", CancellationToken.None, "email");
File.WriteAllText(args[2], Json.Write(new { preparation, speechMs = final.Milliseconds, cleanupMs = result.Milliseconds, finalMs = watch.Elapsed.TotalMilliseconds, raw = finalRaw, text = result.Text, warning = result.Warning, sections = CleanupSections.Split(finalRaw).Count }));
Console.WriteLine($"Speech {final.Milliseconds:F0} ms; cleanup {result.Milliseconds:F0} ms; final wait {watch.Elapsed.TotalMilliseconds:F0} ms; warning: {result.Warning ?? "none"}");
