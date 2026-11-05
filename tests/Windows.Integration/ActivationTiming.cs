using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Murmur.Core;
using Murmur.Services;
using Murmur.Windows;

internal static class ActivationTiming
{
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    public static void Run(string root, string output)
    {
        using var store = new Store(root);
        store.Settings = store.Settings with { Onboarded = true, ToggleShortcut = "Double Caps Lock" };
        using var main = new MainForm(store, true);
        _ = main.Handle;
        using var fixture = new Form { Text = "Murmur activation timing check", Width = 600, Height = 260 };
        var field = new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = "Timing microphone startup. No text will be inserted." };
        fixture.Controls.Add(field);
        fixture.Shown += async (_, _) =>
        {
            var results = new List<object>();
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(45);
                while (DateTime.UtcNow < deadline)
                {
                    Desktop.GetWindowThreadProcessId(Desktop.GetForegroundWindow(), out uint pid);
                    if (pid == Environment.ProcessId) break;
                    await Task.Delay(100);
                }
                Desktop.GetWindowThreadProcessId(Desktop.GetForegroundWindow(), out uint foreground);
                if (foreground != Environment.ProcessId) throw new InvalidOperationException("Owned timing fixture did not gain focus; no recording started.");
                field.Focus();
                var hotkeys = Field<Hotkeys>(main, "hotkeys");
                var recording = Field<RecordingCoordinator>(main, "recording");
                var overlay = Field<RecordingOverlay>(main, "overlay");
                var handle = typeof(Hotkeys).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)!;
                nint packet = Marshal.AllocHGlobal(24);
                try
                {
                    for (int i = 0; i < 24; i++) Marshal.WriteByte(packet, i, 0);
                    Marshal.WriteInt32(packet, 0, 0x14);
                    nint Key(bool down) => (nint)handle.Invoke(hotkeys, [0, (nint)(down ? 0x100 : 0x101), packet])!;
                    for (int iteration = 0; iteration < 5; iteration++)
                    {
                        hotkeys.Reset();
                        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        long trigger = 0, paint = 0;
                        void Painted(object? sender, PaintEventArgs e) { if (trigger != 0 && paint == 0) paint = Stopwatch.GetTimestamp(); }
                        void Changed(object _) { if (recording.HasAudioPackets) ready.TrySetResult(); }
                        overlay.Paint += Painted; recording.Changed += Changed;
                        try
                        {
                            bool consumed = Key(true) == 1; await Task.Delay(50); consumed &= Key(false) == 1;
                            await Task.Delay(90); consumed &= Key(true) == 1; await Task.Delay(50);
                            trigger = Stopwatch.GetTimestamp(); consumed &= Key(false) == 1;
                            await ready.Task.WaitAsync(TimeSpan.FromSeconds(8));
                            var audio = Field<AudioService>(recording, "audio");
                            results.Add(new { iteration, popupPaintMs = Stopwatch.GetElapsedTime(trigger, paint).TotalMilliseconds,
                                firstAudioPacketMs = Stopwatch.GetElapsedTime(trigger, audio.FirstPacketTimestamp).TotalMilliseconds,
                                deviceSelectionMs = Stopwatch.GetElapsedTime(audio.OpenStartedTimestamp, audio.DeviceSelectedTimestamp).TotalMilliseconds,
                                clientCreationMs = Stopwatch.GetElapsedTime(audio.DeviceSelectedTimestamp, audio.ClientCreatedTimestamp).TotalMilliseconds,
                                clientStartMs = Stopwatch.GetElapsedTime(audio.ClientCreatedTimestamp, audio.StartReturnedTimestamp).TotalMilliseconds,
                                startToFirstPacketMs = Stopwatch.GetElapsedTime(audio.StartReturnedTimestamp, audio.FirstPacketTimestamp).TotalMilliseconds,
                                capsEventsConsumed = consumed, bytesCaptured = audio.MicrophoneBytes });
                        }
                        finally { overlay.Paint -= Painted; recording.Changed -= Changed; recording.Cancel(); }
                        await Task.Delay(2000);
                    }
                }
                finally { Marshal.FreeHGlobal(packet); }
                var primer = Field<object>(recording, "primer");
                recording.PrepareMicrophone();
                var preparation = Field<Task>(primer, "preparation");
                await preparation;
                var prepared = preparation.GetType().GetProperty("Result")!.GetValue(preparation)!;
                bool captureNotStarted = Field<Task?>(prepared, "pump") is null;
                await Task.Delay(1000);
                bool abandonedGestureReleased = Field<int>(prepared, "disposed") == 1;
                recording.PrepareMicrophone();
                preparation = Field<Task>(primer, "preparation");
                var canceledStart = recording.Start("dictation"); recording.Cancel(); await canceledStart;
                await preparation; prepared = preparation.GetType().GetProperty("Result")!.GetValue(preparation)!;
                bool canceledStartReleased = recording.State == "idle" && Field<int>(prepared, "disposed") == 1;
                if (!captureNotStarted || !abandonedGestureReleased || !canceledStartReleased) throw new Exception("Microphone preparation lifecycle failed.");
                File.WriteAllText(output, JsonSerializer.Serialize(new { results, captureNotStarted, abandonedGestureReleased, canceledStartReleased, note = "Real WASAPI microphone and actual application handler; key events delivered directly to the hook callback, not physical hardware." }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) { File.WriteAllText(output, JsonSerializer.Serialize(new { error = e.ToString(), results })); Environment.ExitCode = 1; }
            finally { fixture.Close(); }
        };
        Application.Run(fixture);
    }
}
