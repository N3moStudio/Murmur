using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using Murmur.Core;
using Murmur.Services;
using NAudio.Wave;
using System.Windows.Forms;

namespace Murmur.Windows;

public sealed partial class MainForm : Form
{
    private readonly Store store;
    private readonly ModelManager models;
    private readonly TextService text;
    private readonly SpeechService speech;
    private readonly LibraryService library;
    private readonly CalendarService calendar;
    private readonly UpdateService updates;
    private readonly RecordingCoordinator recording;
    private WebView2 web = NewWebView();
    private static WebView2 NewWebView() => new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(245, 244, 240) };
    private DateTime hiddenSince = DateTime.UtcNow;
    private int activeRequests;
    private bool releasingHub;
    private readonly RecordingOverlay overlay = new();
    private readonly Hotkeys hotkeys;
    private readonly NotifyIcon tray = new();
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private readonly EventWaitHandle showSignal;
    private readonly EventWaitHandle shutdownSignal;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? requestWork;
    private bool exiting, ready, initializing;
    private bool allowShow;
    private readonly bool background;
    private WaveOutEvent? playback; private AudioFileReader? player;
    private string? lastTransformOriginal, lastTransformResult;
    private DateTime nextReminderCheck;
    private TaskCompletionSource<bool>? editorFlush;
    public MainForm(Store data, bool startHidden)
    {
        store = data; background = startHidden; allowShow = !startHidden; Text = Brand.Name; Name = Brand.AppId;
        if (store.Settings.SpeechModel != ModelManager.FastEnglish || store.Settings.ShortcutVersion < 1)
            store.Settings = store.Settings with { SpeechModel = ModelManager.FastEnglish,
                ToggleShortcut = store.Settings.ShortcutVersion < 1 && store.Settings.ToggleShortcut == "Double Ctrl" ? "Double Caps Lock" : store.Settings.ToggleShortcut,
                ShortcutVersion = 1 };
        showSignal = new(false, EventResetMode.AutoReset, store.InstanceKey + ".Show");
        shutdownSignal = new(false, EventResetMode.AutoReset, store.InstanceKey + ".Shutdown");
        using (var startup = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
        { bool enabled = startup?.GetValue(Brand.AppId) is string; if (store.Settings.StartAtLogin != enabled) store.Settings = store.Settings with { StartAtLogin = enabled }; }
        // Keep the system caption: Windows owns hit testing, Snap Layouts,
        // accessible caption buttons and work-area/DPI-aware maximize behavior.
        AutoScaleMode = AutoScaleMode.Dpi; FormBorderStyle = FormBorderStyle.Sizable; BackColor = Color.FromArgb(245, 244, 240);
        Size = new(1352, 854); MinimumSize = new(480, 420); StartPosition = FormStartPosition.CenterScreen;
        var area = Screen.PrimaryScreen!.WorkingArea; Size = new(Math.Min(Width, area.Width - 40), Math.Min(Height, area.Height - 40));
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "murmur.ico")); Controls.Add(web);
        models = new(store); text = new(store, models); speech = new(store, models); library = new(store, text); recording = new(store, models, speech, text);
        calendar = new(store); updates = new(store);
        RecoverPending();
        recording.Dispatch = action => { if (!IsDisposed) BeginInvoke(action); };
        recording.Changed += state => { if (!IsDisposed) BeginInvoke(() => { Send("recording", state); UpdateOverlay(); RefreshData(); }); };
        recording.Notice += message => { if (!IsDisposed) BeginInvoke(() => { Send("notice", new { message }); tray.ShowBalloonTip(3000, Brand.Name, message, ToolTipIcon.Info); }); };
        recording.Level += level => { if (!IsDisposed) BeginInvoke(() => { if (Visible) Send("level", new { level }); overlay.UpdateState(recording.State, OverlayLabel(), level); }); };
        models.Progress += progress => { if (!IsDisposed) BeginInvoke(() => Send("download", progress)); };
        hotkeys = new(); hotkeys.Configure(store.Settings.HoldShortcut, store.Settings.ToggleShortcut, store.Settings.CommandShortcut);
        hotkeys.IsRecording = () => recording.State is "starting" or "recording";
        _ = overlay.Handle; // Prepare the tiny native surface without showing it.
        hotkeys.Action += action => { if (!IsDisposed) BeginInvoke(async () => {
            try {
                if (action == "prepare") { recording.PrepareMicrophone(); return; }
                bool showStarting = action is "toggle" or "holdStart" or "commandStart" && recording.State is not ("starting" or "recording" or "processing");
                var operation = recording.Hotkey(action);
                if (showStarting)
                {
                    overlay.Animation = store.Settings.OrbAnimation; overlay.DockEdge = store.Settings.OverlayEdge;
                    overlay.UpdateState("starting", "Starting microphone…"); overlay.Show(); overlay.Update();
                }
                await operation;
            } catch (Exception e) { Send("notice", new { message = e.Message }); tray.ShowBalloonTip(2500, Brand.Name, e.Message, ToolTipIcon.Info); }
            finally { UpdateOverlay(); }
        }); };
        var menu = new ContextMenuStrip(); menu.Items.Add("Open Murmur", null, (_, _) => BringBack()); menu.Items.Add("Start / stop dictation", null, async (_, _) => await Safe(() => recording.Hotkey("toggle"))); menu.Items.Add("Cancel recording", null, (_, _) => recording.Cancel()); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Quit", null, (_, _) => Quit());
        tray.Icon = Icon; tray.Text = Brand.Name + " · Local dictation"; tray.Visible = true; tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => BringBack();
        overlay.Toggle += async () => await Safe(() => recording.Hotkey("toggle")); overlay.Cancel += () => recording.Cancel(); overlay.Open += BringBack;
        clock.Tick += async (_, _) =>
        {
            if (shutdownSignal.WaitOne(0)) { Quit(); return; }
            if (showSignal.WaitOne(0)) BringBack();
            if (!Visible && ready && !releasingHub && !exiting && activeRequests == 0 && recording.State is not ("recording" or "processing") && DateTime.UtcNow - hiddenSince > TimeSpan.FromSeconds(30)) await ReleaseHiddenHub();
            if (recording.State is not ("recording" or "processing")) { text.ReleaseIfIdle(); speech.ReleaseIfIdle(); }
            recording.PrepareDuringPause();
            if (DateTime.UtcNow >= nextReminderCheck)
            {
                nextReminderCheck = DateTime.UtcNow.AddMinutes(1);
                foreach (var item in calendar.DueReminders()) tray.ShowBalloonTip(5000, "Meeting starts soon", item.Title + " · " + item.Start.ToLocalTime().ToString("t") + ". Open Notetaker to join or start recording.", ToolTipIcon.Info);
            }
            if (recording.State == "recording") { UpdateOverlay(); if (Visible) Send("tick", new { elapsed = recording.Elapsed }); if (recording.Elapsed >= (recording.Mode == "meeting" ? store.Settings.MeetingLimitMinutes * 60 : 1200)) await Safe(recording.Stop); }
        };
        clock.Start(); Load += async (_, _) =>
        {
            var bounds = Screen.FromControl(this).WorkingArea; double scale = DeviceDpi / 96d;
            Size = new(Math.Min((int)(1352 * scale), bounds.Width - 30), Math.Min((int)(854 * scale), bounds.Height - 30));
            CenterToScreen();
            if (background && !allowShow) { Hide(); UpdateOverlay(); } else await Initialize();
        };
        FormClosing += (_, e) => { if (!exiting) { e.Cancel = true; Hide(); Send("visibility", new { active = false }); } };
        Resize += (_, _) => { if (ready) Send("visibility", new { active = Visible && WindowState != FormWindowState.Minimized }); };
        VisibleChanged += (_, _) => SetWebActivity();
        Resize += (_, _) => SetWebActivity();
        SystemEvents.PowerModeChanged += PowerChanged;
    }
    private async Task Initialize()
    {
        if (initializing || web.CoreWebView2 is not null) return;
        initializing = true;
        try
        {
            var options = new CoreWebView2EnvironmentOptions();
            if (Environment.GetEnvironmentVariable("MURMUR_DEVTOOLS") == "1") options.AdditionalBrowserArguments = "--remote-debugging-port=9332";
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(store.Root, "webview"), options);
            await web.EnsureCoreWebView2Async(env);
            if (web.CoreWebView2 is null) throw new InvalidOperationException("The window could not initialize.");
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("murmur.local", Path.Combine(AppContext.BaseDirectory, "web"), CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.Settings.AreDevToolsEnabled = Environment.GetEnvironmentVariable("MURMUR_DEVTOOLS") == "1";
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false; web.CoreWebView2.Settings.IsStatusBarEnabled = false; web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            web.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            web.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            web.CoreWebView2.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://murmur.local/", StringComparison.Ordinal)) e.Cancel = true; };
            web.CoreWebView2.WebMessageReceived += MessageReceived;
            web.CoreWebView2.Navigate("https://murmur.local/index.html");
            UpdateOverlay(); store.Prune();
        }
        catch (WebView2RuntimeNotFoundException) { MessageBox.Show("Microsoft Edge WebView2 Runtime is required. Run Murmur Setup to install it, then reopen Murmur.", Brand.Name); Quit(); }
        finally { initializing = false; }
    }
    private void SetWebActivity()
    {
        hiddenSince = DateTime.UtcNow;
        var core = web.CoreWebView2; if (core is null) return;
        bool active = Visible && WindowState != FormWindowState.Minimized;
        Send("visibility", new { active });
        // Low mode preserves pending save/download responses while reducing cached UI memory.
        core.MemoryUsageTargetLevel = active ? CoreWebView2MemoryUsageTargetLevel.Normal : CoreWebView2MemoryUsageTargetLevel.Low;
    }
    private async Task ReleaseHiddenHub()
    {
        releasingHub = true;
        try
        {
            editorFlush = new(TaskCreationOptions.RunContinuationsAsynchronously); Send("flushEditor", new { });
            bool saved; try { saved = await editorFlush.Task.WaitAsync(TimeSpan.FromSeconds(2)); } catch (TimeoutException) { saved = false; }
            editorFlush = null;
            if (!saved || Visible || exiting || activeRequests > 0 || recording.State is "recording" or "processing") { hiddenSince = DateTime.UtcNow; return; }
            ready = false; Controls.Remove(web); web.Dispose(); web = NewWebView(); Controls.Add(web);
        }
        finally { releasingHub = false; }
    }
    private async void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!e.Source.StartsWith("https://murmur.local/", StringComparison.Ordinal)) return;
        string? requestId = null;
        activeRequests++;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson); var message = document.RootElement;
            requestId = message.GetProperty("id").GetString(); string action = message.GetProperty("action").GetString()!;
            var args = message.TryGetProperty("args", out var a) ? a.Clone() : JsonDocument.Parse("{}").RootElement.Clone();
            var result = await Execute(action, args); Send("response", new { id = requestId, result });
        }
        catch (Exception ex) { Send("response", new { id = requestId, error = ex is OperationCanceledException ? "Cancelled." : ex.Message }); }
        finally { activeRequests--; }
    }
    private void Send(string type, object data) { if (web.CoreWebView2 is not null) web.CoreWebView2.PostWebMessageAsJson(Json.Write(new { type, data })); }
    private object Snapshot() => new
    {
        name = Brand.Name, publisher = Brand.Publisher, version = Brand.Version, architecture = RuntimeInformation.ProcessArchitecture.ToString(), settings = store.Settings,
        history = store.List<HistoryItem>("history", 300), dictionary = store.List<LibraryEntry>("dictionary"), snippets = store.List<LibraryEntry>("snippet"), transforms = store.List<Transform>("transform"), folders = store.List<Folder>("folder"),
        meetings = store.List<Meeting>("meeting", 500).Select(m => new { m.Id, m.Title, m.Created, m.FolderId, m.Status, m.Duration, hasSummary = m.Summary.Length > 0 }), models = models.Status(),
        recording = new { state = recording.State, mode = recording.Mode, id = recording.Id }, pending = store.List<JsonElement>("pending"), scratchpad = store.Get<string>("scratchpad", "main") ?? "",
        lowPower = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline, providerKeySaved = Secrets.Read(store.Root, "provider") is not null,
        mcpCommand = Environment.ProcessPath + " --mcp", calendarConnections = calendar.Connections(), calendarEvents = calendar.Upcoming()
    };
    private void RefreshData() { if (ready && Visible) Send("snapshot", Snapshot()); }
    private void RecoverPending()
    {
        foreach (var pending in store.List<JsonElement>("pending"))
        {
            string id = pending.GetProperty("id").GetString()!;
            var paths = pending.GetProperty("paths").EnumerateArray().Select(x => x.GetString()!).Where(p => store.IsRecordingPath(p) && File.Exists(p)).ToList();
            if (pending.GetProperty("mode").GetString() == "meeting")
            {
                var meeting = store.Get<Meeting>("meeting", id);
                if (meeting is not null) store.Save("meeting", id, meeting with { Status = "interrupted", Audio = (meeting.Audio ?? []).Concat(paths).Distinct().ToList() });
            }
            else if (store.Get<HistoryItem>("history", id) is null && paths.Count > 0)
                store.Save("history", id, new HistoryItem(id, pending.GetProperty("started").GetDateTimeOffset(), "", "", paths[0], pending.GetProperty("target").GetString() ?? "", "failed", 0, 0, 0, "An interrupted recording was recovered. Choose Retry transcription."));
            store.Delete("pending", id);
        }
    }
    protected override void SetVisibleCore(bool value)
    {
        // Hiding inside Load is too early: WinForms can show the window again
        // after Load returns. Create only its message handle for tray/hotkeys.
        if (value && !allowShow) { if (!IsHandleCreated) CreateHandle(); value = false; }
        base.SetVisibleCore(value);
    }
    private void BringBack() { allowShow = true; Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); if (web.CoreWebView2 is null) _ = Initialize(); else { RefreshData(); SetWebActivity(); } }
    private string OverlayLabel() => recording.State switch { "recording" => !recording.HasAudioPackets ? "Starting microphone…" : (recording.Mode == "meeting" ? "Meeting " : "Listening ") + TimeSpan.FromSeconds(recording.Elapsed).ToString(@"mm\:ss"), "processing" => "Thinking…", "completed" => "Done", "failed" => "Open to recover", _ => "Ready" };
    private void UpdateOverlay() { overlay.Animation = store.Settings.OrbAnimation; overlay.DockEdge = store.Settings.OverlayEdge; overlay.UpdateState(recording.State, OverlayLabel()); if (InteractionRules.ShowOverlay(recording.State)) overlay.Show(); else overlay.Hide(); }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (!IsDisposed) BeginInvoke(() => { hotkeys.Reset(); if (e.Mode == PowerModes.Suspend && recording.State == "recording") _ = Safe(recording.Stop); Send("power", new { lowPower = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline }); });
    }
    private async Task Safe(Func<Task> action) { try { await action(); } catch (Exception e) { Send("notice", new { message = e.Message }); tray.ShowBalloonTip(3000, Brand.Name, e.Message, ToolTipIcon.Warning); } }
    private async void Quit()
    {
        if (exiting) return; exiting = true;
        if (ready && web.CoreWebView2 is not null)
        {
            editorFlush = new(TaskCreationOptions.RunContinuationsAsynchronously); Send("flushEditor", new { });
            bool saved;
            try { saved = await editorFlush.Task.WaitAsync(TimeSpan.FromSeconds(2)); } catch (TimeoutException) { saved = false; }
            editorFlush = null;
            if (!saved) { exiting = false; BringBack(); Send("notice", new { message = "Your editor has not finished saving. Please try Quit again once it is saved." }); return; }
        }
        lifetime.Cancel(); requestWork?.Cancel(); recording.Dispose(); overlay.Close(); tray.Visible = false; Close();
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var theme = store?.Settings.Theme ?? "system";
        SetCaptionTheme(theme == "dark" || theme == "system" &&
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int light && light == 0);
    }
    private void SetCaptionTheme(bool dark)
    {
        if (!IsHandleCreated) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref value, sizeof(int));
        int color = dark ? 0x202425 : 0xf0f4f5; // COLORREF: matches the hub's warm frame.
        DwmSetWindowAttribute(Handle, 35, ref color, sizeof(int));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { SystemEvents.PowerModeChanged -= PowerChanged; clock.Dispose(); hotkeys.Dispose(); tray.Dispose(); overlay.Dispose(); recording.Dispose(); speech.Dispose(); text.Dispose(); calendar.Dispose(); updates.Dispose(); playback?.Dispose(); player?.Dispose(); requestWork?.Dispose(); lifetime.Dispose(); showSignal.Dispose(); shutdownSignal.Dispose(); }
        base.Dispose(disposing);
    }
}
