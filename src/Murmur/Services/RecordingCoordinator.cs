using Murmur.Core;
using Murmur.Windows;
using NAudio.Wave;

namespace Murmur.Services;

public sealed class RecordingCoordinator(Store store, ModelManager models, SpeechService speech, TextService text) : IDisposable
{
    private AudioService? audio;
    private CancellationTokenSource? work;
    private readonly RecordingMachine machine = new();
    private Target? target;
    private Task<Target>? targetCapture;
    private string id = "", mode = "dictation", selected = "";
    private bool locked;
    private bool starting;
    private readonly MicrophonePrimer primer = new();
    private long generation;
    private DateTimeOffset started;
    private DateTimeOffset lastTap;
    private List<string> currentPaths = [];
    private sealed record Prepared(long Generation, string Raw, string Text, double Milliseconds, string? Warning, string Process, string Category);
    private volatile Prepared? prepared;
    private Task? preparation;
    private CancellationTokenSource? preparationCancel;
    private DateTimeOffset lastPreparation;
    private double lastPreparedElapsed;
    private long preparedSoundRevision;
    public string State => starting && machine.State is not (RecordingState.Recording or RecordingState.Processing) ? "starting" : machine.State.ToString().ToLowerInvariant();
    public string Mode => mode;
    public string Id => id;
    public double Elapsed => (DateTimeOffset.UtcNow - started).TotalSeconds;
    public bool HasAudioPackets => audio is { MicrophoneBytes: > 0 };
    public event Action<object>? Changed;
    public event Action<float>? Level;
    public event Action<string>? Notice;
    public Action<Action>? Dispatch;
    private void Emit(string? error = null) => Changed?.Invoke(new { state = State, mode, id, elapsed = Elapsed, error });
    public async Task Hotkey(string action)
    {
        switch (action)
        {
            case "cancel": if (State is "starting" or "recording" or "processing") Cancel(); break;
            case "holdStart":
                if (State == "recording" && locked) { await Stop(); break; }
                if ((DateTimeOffset.UtcNow - lastTap).TotalMilliseconds < 500 && State == "recording") { locked = true; Emit(); break; }
                if (State != "recording") await Start("dictation", false);
                break;
            case "commandStart":
                if (State == "recording" && Elapsed < .6) { try { selected = await Desktop.CopySelection(target!); mode = "command"; Emit(); } catch { Cancel(); throw; } }
                else await Start("command", false);
                break;
            case "holdStop":
                if (State != "recording" || locked) break;
                if (Elapsed < .18) { lastTap = DateTimeOffset.UtcNow; var thisGeneration = generation; await Task.Delay(500); if (generation == thisGeneration && State == "recording" && !locked) Cancel(); }
                else await Stop();
                break;
            case "toggle":
                if (starting) { Cancel(); break; }
                if (State == "recording" && !locked && Elapsed < .6) { locked = true; Emit(); }
                else if (State == "recording") await Stop(); else await Start("dictation", true);
                break;
        }
    }
    public void PrepareMicrophone()
    {
        if (State is not ("starting" or "recording" or "processing")) primer.Prepare(store.Settings.Microphone);
    }
    public async Task Start(string recordingMode, bool handsFree = true, string? meetingId = null)
    {
        if (starting) return;
        starting = true;
        try
        {
        if (State is "recording" or "processing") throw new InvalidOperationException("Finish or cancel the current recording first.");
        if (!models.Available(store.Settings.SpeechModel)) throw new InvalidOperationException("Use Download all in Settings → Dictation setup first.");
        target = Desktop.CaptureWindow(); selected = "";
        var originalTarget = target;
        // UI Automation can stall on a busy app. Start the microphone without
        // waiting for it, and resolve the original field before insertion.
        targetCapture = Task.Run(() => Desktop.Capture(originalTarget));
        mode = recordingMode; locked = handsFree; id = meetingId ?? Guid.NewGuid().ToString("N");
        work = new(); generation++; started = DateTimeOffset.UtcNow; prepared = null; preparedSoundRevision = 0; currentPaths = []; lastPreparedElapsed = 0; text.ClearCleanupCache(); speech.ClearPreparedAudio();
        var ownGeneration = generation; var token = work.Token;
        var microphone = await primer.Take(store.Settings.Microphone);
        if (generation != ownGeneration || token.IsCancellationRequested) { microphone.Dispose(); return; }
        if (recordingMode == "command")
        {
            try { target = await targetCapture.WaitAsync(TimeSpan.FromSeconds(5), token); selected = await Desktop.CopySelection(target); }
            catch { microphone.Dispose(); throw; }
        }
        var folder = Path.Combine(store.Recordings, id, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        audio = new(); audio.Level += x => Level?.Invoke(x); audio.Fault += message => Dispatch?.Invoke(() => { Notice?.Invoke(message); _ = Stop(); });
        audio.FirstPacket += () => Dispatch?.Invoke(() => Emit());
        try
        {
            audio.Start(folder, store.Settings.Microphone, mode == "meeting" && store.Settings.MeetingSystemAudio, microphone); machine.Start();
            currentPaths = Directory.GetFiles(folder, "*.wav").ToList();
            store.Save("pending", id, new { id, mode, paths = currentPaths, started, target = target.Process });
            if (mode == "meeting") { var meeting = store.Get<Meeting>("meeting", id) ?? new(id, "Untitled note", started); store.Save("meeting", id, meeting with { Status = "recording" }); }
            Emit();
            _ = ResolveTarget(generation, targetCapture);
            // Let the native popup paint before starting any model processes.
            await Task.Yield();
            if (generation != ownGeneration || token.IsCancellationRequested) return;
            if (mode == "command" || (mode == "dictation" && store.Settings.Cleanup != "none")) _ = text.Prepare(work.Token);
            _ = speech.Prepare(work.Token);
        }
        catch { audio.Dispose(); audio = null; machine.Cancel(); Emit(); throw; }
        }
        finally { starting = false; if (State is not ("recording" or "processing")) { work?.Dispose(); work = null; Emit(); } }
    }
    private async Task ResolveTarget(long ownGeneration, Task<Target> capture)
    {
        try { var result = await capture.WaitAsync(TimeSpan.FromSeconds(5)); if (generation == ownGeneration) target = result; }
        catch (Exception) { /* The original non-editable window remains recovery-only. */ }
    }
    public async Task Stop()
    {
        if (!machine.Stop() || audio is null || work is null) return;
        preparationCancel?.Cancel();
        var ownPreparation = preparation;
        var ownGeneration = generation; var ownWork = work; var ct = ownWork.Token; var ownId = id; var ownMode = mode; double duration = Elapsed;
        var ownAudio = audio; audio = null;
        var ownTarget = target; var ownCapture = targetCapture; var ownSelected = selected; var ownStarted = started; var ownPaths = currentPaths.ToList();
        if (ownMode == "dictation")
        {
            ownTarget = Desktop.CaptureWindow();
            var stoppedWindow = ownTarget;
            ownCapture = Task.Run(() => Desktop.Capture(stoppedWindow), ct);
        }
        Emit();
        try
        {
            var tracks = await ownAudio.Stop(); ct.ThrowIfCancellationRequested();
            if (ownPreparation is not null) await ownPreparation;
            var segments = new List<Segment>(); double milliseconds = 0;
            foreach (var track in tracks)
            {
                var result = await speech.Transcribe(track.Path, track.Speaker, ct); ct.ThrowIfCancellationRequested();
                var labeled = result.Segments;
                if (ownMode == "meeting" && store.Settings.IdentifySpeakers && labeled.Count > 0)
                {
                    try { labeled = await new SpeakerService(store, models).Label(track.Path, labeled, track.Speaker, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { Notice?.Invoke(ex.Message); }
                }
                segments.AddRange(labeled); milliseconds += result.Milliseconds;
            }
            if (generation != ownGeneration) return;
            segments = segments.OrderBy(s => s.Start).ToList();
            if (ownMode == "meeting") await FinishMeeting(ownId, tracks.Select(t => t.Path).ToList(), segments, duration, ct);
            else
            {
                string raw = string.Join(" ", segments.Select(s => s.Text));
                if (raw.Length == 0)
                {
                    const string message = "No clear speech was detected. Check your microphone in Settings. This recording is saved in History for playback or retry.";
                    store.Save("history", ownId, new HistoryItem(ownId, ownStarted, "", "", tracks.FirstOrDefault().Path, ownTarget?.Process ?? "", "failed", duration, milliseconds, 0, message));
                    Notice?.Invoke(message); machine.Finish(false); store.Delete("pending", ownId); Emit(); return;
                }
                if (ownCapture is not null)
                    try { ownTarget = await ownCapture.WaitAsync(TimeSpan.FromSeconds(5), ct); } catch (TimeoutException) { }
                if (ownMode == "dictation" && ownTarget?.Editable != true) ownTarget = await Desktop.ResolveDictationTarget(ownTarget, ct);
                string result; double cleanupMs = 0; string? warning = null;
                store.Save("history", ownId, new HistoryItem(ownId, ownStarted, raw, raw, tracks.FirstOrDefault().Path, ownTarget?.Process ?? "", "ready", duration, milliseconds, 0, "Processing has not finished."));
                if (ownMode == "command") result = await text.Generate("Edit the supplied text according to the spoken instruction. Return only the edited text. Do not invent facts. Preserve details unless the instruction explicitly changes them.", "Instruction: " + raw + "\nOriginal text:\n" + ownSelected, ct);
                else if (prepared is { } cached && cached.Generation == ownGeneration && cached.Raw == raw && cached.Process == (ownTarget?.Process ?? "") && cached.Category == (ownTarget?.Category ?? "other")) { result = cached.Text; cleanupMs = 0; warning = cached.Warning; }
                else { var clean = await text.Cleanup(raw, ownTarget?.Process ?? "", ct, ownTarget?.Category ?? "other"); result = clean.Text; cleanupMs = clean.Milliseconds; warning = clean.Warning; }
                ct.ThrowIfCancellationRequested(); if (generation != ownGeneration) return;
                var item = new HistoryItem(ownId, ownStarted, result, raw, tracks.FirstOrDefault().Path, ownTarget?.Process ?? "", "ready", duration, milliseconds, cleanupMs);
                // Persist recovery BEFORE attempting an external paste.
                store.Save("history", ownId, item);
                if (ownMode == "dictation") ownTarget = await Desktop.ResolveDictationTarget(ownTarget, ct);
                var error = ownTarget is null ? "Choose an editable target, then retry." : await Desktop.Paste(ownTarget, result, store.Settings.SubmitAfterInsert, ct);
                item = item with { Status = error is null ? "inserted" : "ready", Error = error ?? warning };
                if (!store.Settings.RetainAudio && error is null) { store.DeleteRecording(item.Audio); item = item with { Audio = null }; }
                store.Save("history", ownId, item);
                if (error is not null) Notice?.Invoke(error); else if (warning is not null) Notice?.Invoke(warning);
                if (!store.Settings.RetainHistory && error is null) store.Delete("history", ownId);
            }
            store.Delete("pending", ownId); if (generation == ownGeneration) { machine.Finish(true); Emit(); }
        }
        catch (OperationCanceledException)
        {
            // Audio remains recoverable; never delete a file still used by an inference worker.
            if (ownMode != "meeting")
            {
                var saved = store.Get<HistoryItem>("history", ownId) ?? new HistoryItem(ownId, ownStarted, "", "", ownPaths.FirstOrDefault(), ownTarget?.Process ?? "", "cancelled", duration, 0, 0);
                store.Save("history", ownId, saved with { Status = "cancelled", Error = "Processing cancelled. Original text and audio remain available until you delete them." });
            }
            store.Delete("pending", ownId);
            if (generation == ownGeneration) { machine.Cancel(); Emit(); }
        }
        catch (Exception ex)
        {
            if (generation != ownGeneration) return;
            if (ownMode == "meeting") { var m = store.Get<Meeting>("meeting", ownId); if (m is not null) store.Save("meeting", ownId, m with { Status = "failed", Audio = (m.Audio ?? []).Concat(ownPaths).Distinct().ToList() }); }
            else { var saved = store.Get<HistoryItem>("history", ownId) ?? new HistoryItem(ownId, ownStarted, "", "", ownPaths.FirstOrDefault(), ownTarget?.Process ?? "", "failed", duration, 0, 0); store.Save("history", ownId, saved with { Status = "failed", Error = ex.Message }); }
            machine.Finish(false); Emit(ex.Message); Notice?.Invoke(ex.Message);
        }
        finally { ownAudio.Dispose(); ownWork.Dispose(); if (generation == ownGeneration) work = null; }
    }
    // Prepare during pauses and periodically during long speech. Final recognition runs on the complete
    // recording and must match exactly before reusing any prepared cleanup.
    public void PrepareDuringPause()
    {
        if (State == "recording" && audio is { MicrophoneBytes: 0 } && Elapsed > 4) { Notice?.Invoke("The microphone has not delivered audio. Check microphone permissions or select another input in Settings."); _ = Stop(); return; }
        if (mode != "dictation" || State != "recording" || audio is null || work is null || Elapsed < 2 || audio.SoundRevision == preparedSoundRevision || preparation is { IsCompleted: false } || DateTimeOffset.UtcNow - lastPreparation < TimeSpan.FromSeconds(8)) return;
        // During continuous speech, prepare periodically too. Reuse only exact
        // sections after full final recognition; never paste a speculative result.
        if (audio.QuietMilliseconds < 550 && Elapsed - lastPreparedElapsed < 20) return;
        preparedSoundRevision = audio.SoundRevision;
        lastPreparedElapsed = Elapsed;
        lastPreparation = DateTimeOffset.UtcNow;
        preparationCancel?.Dispose(); preparationCancel = CancellationTokenSource.CreateLinkedTokenSource(work.Token);
        var token = preparationCancel.Token; var ownGeneration = generation; var ownAudio = audio; var ownTarget = target;
        string snapshot = Path.Combine(store.Recordings, Guid.NewGuid() + ".preview.wav");
        preparation = Task.Run(async () =>
        {
            try
            {
                if (!ownAudio.SnapshotMicrophone(snapshot, token)) return;
                var recognition = await speech.Transcribe(snapshot, "You", token);
                string raw = string.Join(" ", recognition.Segments.Select(s => s.Text));
                if (raw.Length == 0 || prepared is { } existing && existing.Generation == ownGeneration && existing.Raw == raw) return;
                var clean = await text.Cleanup(raw, ownTarget?.Process ?? "", token, ownTarget?.Category ?? "other");
                token.ThrowIfCancellationRequested();
                if (generation == ownGeneration && State == "recording") prepared = new(ownGeneration, raw, clean.Text, clean.Milliseconds, clean.Warning, ownTarget?.Process ?? "", ownTarget?.Category ?? "other");
            }
            catch (Exception) { /* Speculation cannot fail the actual recording. */ }
            finally { store.DeleteRecording(snapshot); }
        }, CancellationToken.None);
    }
    private async Task FinishMeeting(string meetingId, List<string> paths, List<Segment> segments, double duration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var meeting = store.Get<Meeting>("meeting", meetingId) ?? new(meetingId, "Untitled note", started);
        double offset = meeting.Duration;
        var combined = (meeting.Segments ?? []).Concat(segments.Select(s => s with { Start = s.Start + offset, End = s.End + offset })).ToList();
        var transcript = string.Join("\n", combined.Select(s => $"[{TimeSpan.FromSeconds(s.Start):hh\\:mm\\:ss}] {s.Speaker}: {s.Text}"));
        meeting = meeting with { Segments = combined, Transcript = transcript, Audio = (meeting.Audio ?? []).Concat(paths).ToList(), Duration = offset + duration, Status = "transcribed" };
        store.Save("meeting", meetingId, meeting);
        if (transcript.Length == 0) { store.Save("meeting", meetingId, meeting with { Status = "silent" }); Notice?.Invoke("No speech was detected in the meeting audio."); return; }
        if (!meeting.SummaryEdited)
        {
            var summary = await text.Summary(transcript, ct); ct.ThrowIfCancellationRequested();
            // Re-read to preserve notes edited while the model was running.
            var current = store.Get<Meeting>("meeting", meetingId); if (current is not null && !current.SummaryEdited) store.Save("meeting", meetingId, current with { Summary = summary, Status = "ready" });
        }
    }
    public void Cancel()
    {
        bool wasProcessing = State == "processing";
        generation++; preparationCancel?.Cancel(); work?.Cancel(); audio?.Dispose(); audio = null;
        if (mode == "meeting") { var m = store.Get<Meeting>("meeting", id); if (m is not null) store.Save("meeting", id, m with { Status = "interrupted", Audio = (m.Audio ?? []).Concat(currentPaths).Distinct().ToList() }); }
        else if (!wasProcessing) { foreach (var path in currentPaths) store.DeleteRecording(path); store.Delete("pending", id); work?.Dispose(); }
        work = null;
        machine.Cancel(); Emit();
    }
    public async Task Import(string path, bool meeting, CancellationToken ct)
    {
        if (State is "recording" or "processing") throw new InvalidOperationException("Finish the current recording first.");
        id = Guid.NewGuid().ToString("N"); mode = meeting ? "meeting" : "dictation"; generation++; started = DateTimeOffset.UtcNow;
        var ownId = id; var ownGeneration = generation; var ownStarted = started;
        var ownWork = CancellationTokenSource.CreateLinkedTokenSource(ct); work = ownWork; ct = ownWork.Token; currentPaths = [];
        machine.Start(); machine.Stop(); Emit();
        var folder = Path.Combine(store.Recordings, ownId); Directory.CreateDirectory(folder); var copied = Path.Combine(folder, "import.wav");
        bool converted = false; double duration = 0;
        try
        {
            await Task.Run(() => AudioService.ToMono16(path, copied, ct), ct); ct.ThrowIfCancellationRequested();
            converted = true; currentPaths = [copied];
            using (var reader = new WaveFileReader(copied)) duration = reader.TotalTime.TotalSeconds;
            store.Save("pending", ownId, new { id = ownId, mode, paths = new[] { copied }, started = ownStarted, target = "Imported audio" });
            if (meeting) store.Save("meeting", ownId, new Meeting(ownId, Path.GetFileNameWithoutExtension(path), ownStarted, Status: "processing", Audio: [copied]));
            var result = await speech.Transcribe(copied, "Speaker 1", ct);
            ct.ThrowIfCancellationRequested();
            if (meeting)
            {
                var segments = result.Segments;
                if (store.Settings.IdentifySpeakers && segments.Count > 0)
                {
                    try { segments = await new SpeakerService(store, models).Label(copied, segments, "Recording", ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { Notice?.Invoke(ex.Message); }
                }
                await FinishMeeting(ownId, [], segments, duration, ct);
            }
            else
            {
                var raw = string.Join(" ", result.Segments.Select(s => s.Text));
                var saved = new HistoryItem(ownId, ownStarted, raw, raw, copied, "Imported audio", "ready", duration, result.Milliseconds, 0);
                store.Save("history", ownId, saved);
                var clean = raw.Length == 0 ? (Text: "", Milliseconds: 0d, Warning: (string?)null) : await text.Cleanup(raw, "import", ct);
                ct.ThrowIfCancellationRequested(); store.Save("history", ownId, saved with { Text = clean.Text, CleanupMs = clean.Milliseconds, Error = clean.Warning });
            }
            store.Delete("pending", ownId); if (generation == ownGeneration) { machine.Finish(true); Emit(); }
        }
        catch (Exception ex)
        {
            if (!converted) store.DeleteRecording(copied);
            else if (!meeting)
            {
                var saved = store.Get<HistoryItem>("history", ownId) ?? new HistoryItem(ownId, ownStarted, "", "", copied, "Imported audio", "failed", duration, 0, 0);
                store.Save("history", ownId, saved with { Status = ex is OperationCanceledException ? "cancelled" : "failed", Error = ex is OperationCanceledException ? "Import cancelled. Converted audio is available for retry." : ex.Message });
            }
            else { var saved = store.Get<Meeting>("meeting", ownId); if (saved is not null) store.Save("meeting", ownId, saved with { Status = ex is OperationCanceledException ? "interrupted" : "failed" }); }
            store.Delete("pending", ownId);
            if (generation == ownGeneration) { if (ex is OperationCanceledException) machine.Cancel(); else machine.Finish(false); Emit(ex is OperationCanceledException ? null : ex.Message); }
            throw;
        }
        finally { ownWork.Dispose(); if (generation == ownGeneration) work = null; }
    }
    public void Dispose() { generation++; primer.Dispose(); preparationCancel?.Cancel(); work?.Cancel(); audio?.Dispose(); work?.Dispose(); }
}
