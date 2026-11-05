using Murmur.Core;
using Murmur.Services;
using Murmur.Windows;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Murmur;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        string? root = Option(args, "--data-dir");
        try
        {
            using var store = new Store(root);
            if (args.Contains("--shutdown")) { if (EventWaitHandle.TryOpenExisting(store.InstanceKey + ".Shutdown", out var shutdown)) { using (shutdown) shutdown.Set(); Thread.Sleep(3500); } return; }
            if (args.Contains("--mcp")) { McpServer.Run(store).GetAwaiter().GetResult(); return; }
            if (Option(args, "--diarize") is { } speakerAudio)
            {
                var output = Option(args, "--output") ?? speakerAudio + ".speakers.json";
                var turns = SpeakerService.RunWorker(store, new ModelManager(store), speakerAudio, output + ".wav");
                File.WriteAllText(output, Json.Write(turns)); return;
            }
            if (Option(args, "--cleanup") is { } textFile)
            {
                var models = new ModelManager(store); using var engine = new TextService(store, models);
                var result = engine.Cleanup(File.ReadAllText(textFile), "fixture", CancellationToken.None, Option(args, "--category") ?? "other").GetAwaiter().GetResult();
                File.WriteAllText(Option(args, "--output") ?? textFile + ".json", Json.Write(new { architecture = RuntimeInformation.ProcessArchitecture.ToString(), milliseconds = result.Milliseconds, text = result.Text, warning = result.Warning })); return;
            }
            if (Option(args, "--summarize") is { } transcriptFile)
            {
                using var engine = new TextService(store, new ModelManager(store)); var watch = System.Diagnostics.Stopwatch.StartNew();
                var summary = engine.Summary(File.ReadAllText(transcriptFile), CancellationToken.None).GetAwaiter().GetResult();
                File.WriteAllText(Option(args, "--output") ?? transcriptFile + ".json", Json.Write(new { text = summary, milliseconds = watch.Elapsed.TotalMilliseconds })); return;
            }
            if (Option(args, "--transcribe") is { } input)
            {
                var models = new ModelManager(store); using var speech = new SpeechService(store, models);
                using var text = new TextService(store, models);
                var model = Option(args, "--speech-model");
                if (args.Contains("--warm")) { speech.Prepare(CancellationToken.None, model).GetAwaiter().GetResult(); if (args.Contains("--with-cleanup")) text.Prepare(CancellationToken.None).GetAwaiter().GetResult(); }
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var result = speech.Transcribe(input, "Speaker 1", CancellationToken.None, model).GetAwaiter().GetResult();
                string raw = string.Join(" ", result.Segments.Select(s => s.Text));
                var clean = args.Contains("--with-cleanup") && raw.Length > 0 ? text.Cleanup(raw, "fixture", CancellationToken.None, Option(args, "--category") ?? "other").GetAwaiter().GetResult() : (Text: raw, Milliseconds: 0d, Warning: (string?)null);
                File.WriteAllText(Option(args, "--output") ?? input + ".json", Json.Write(new { architecture = RuntimeInformation.ProcessArchitecture.ToString(), milliseconds = result.Milliseconds, segments = result.Segments, text = clean.Text, cleanupMs = clean.Milliseconds, warning = clean.Warning, totalMs = watch.Elapsed.TotalMilliseconds })); return;
            }
            ApplicationConfiguration.Initialize();
            using var instance = new Mutex(true, store.InstanceKey, out bool isNew);
            if (!isNew) { if (EventWaitHandle.TryOpenExisting(store.InstanceKey + ".Show", out var signal)) { using (signal) signal.Set(); } return; }
            Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message, Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            using var form = new MainForm(store, args.Contains("--background"));
            Application.Run(form);
        }
        catch (Exception ex)
        {
            if (Option(args, "--output") is { } output) File.WriteAllText(output, Json.Write(new { error = ex.Message, architecture = RuntimeInformation.ProcessArchitecture.ToString() }));
            else MessageBox.Show(ex.Message, Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
        }
    }
    private static string? Option(string[] args, string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
}
