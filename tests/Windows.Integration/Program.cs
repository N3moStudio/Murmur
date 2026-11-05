using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Murmur.Windows;
using Murmur.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using Application = System.Windows.Forms.Application;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        Console.InputEncoding = new System.Text.UTF8Encoding(false);
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        ApplicationConfiguration.Initialize();
        if (args.Contains("--overlay-motion")) { OverlayMotion.Run(args[1]); return; }
        if (args.Contains("--activation")) { ActivationTiming.Run(args[1], args[2]); return; }
        if (args.Contains("--fixture")) { Application.Run(new Fixture(args.Contains("--web"))); return; }
        using var host = new Form { Text = "Murmur insertion verification", ShowInTaskbar = false, Width = 1, Height = 1, Opacity = 0 };
        host.Shown += async (_, _) => { host.Hide(); try { await Run(args[0], args.Contains("--web-only")); } catch (Exception ex) { File.WriteAllText(args[0], JsonSerializer.Serialize(new { error = ex.ToString() })); Environment.ExitCode = 1; } finally { host.Close(); } };
        Application.Run(host);
    }
    private static async Task Run(string output, bool webOnly)
    {
        var results = new List<object>(); int failures = 0;
        void Check(string name, bool passed, string? detail = null) { results.Add(new { name, passed, detail }); if (!passed) failures++; }
        foreach (bool web in webOnly ? new[] { true } : new[] { false, true })
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") info.ArgumentList.Add(typeof(Program).Assembly.Location);
            info.ArgumentList.Add("--fixture"); if (web) info.ArgumentList.Add("--web");
            using var child = Process.Start(info)!;
            _ = child.StandardError.ReadToEndAsync();
            Target CaptureOwned()
            {
                var target = Desktop.Capture(); Desktop.GetWindowThreadProcessId(target.Window, out uint focusedProcess);
                if (focusedProcess != child.Id) throw new InvalidOperationException($"Fixture did not gain foreground focus (expected process {child.Id}, observed {target.Process} / {focusedProcess}). No test input was sent to that target.");
                return target;
            }
            async Task<JsonElement> Command(string op, string field = "plain", string text = "", int start = 0, int length = 0)
            {
                await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { op, field, text, start, length })); await child.StandardInput.FlushAsync();
                string line = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)) ?? throw new IOException("Fixture closed.");
                using var doc = JsonDocument.Parse(line); var result = doc.RootElement.Clone();
                if (result.TryGetProperty("error", out var error)) throw new Exception(error.GetString());
                return result;
            }
            try
            {
                await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Console.WriteLine("Awaiting focus: " + (web ? "Murmur browser insertion checks" : "Murmur native insertion checks"));
                var focusDeadline = DateTime.UtcNow.AddSeconds(45);
                while (DateTime.UtcNow < focusDeadline)
                {
                    Desktop.GetWindowThreadProcessId(Desktop.GetForegroundWindow(), out uint foreground);
                    if (foreground == child.Id) break;
                    await Task.Delay(100);
                }
                foreach (var field in new[] { "plain", "rich" })
                {
                    string label = (web ? "WebView2 " : "native ") + field;
                    await Command("set", field); await Task.Delay(150);
                    var target = CaptureOwned();
                    Check(label + " editable target", target.Editable);
                    string value = "Hello, Naïth.\r\nKeep 37 & café.";
                    string? error = await Desktop.Paste(target, value, false);
                    var actual = await Command("get", field);
                    Check(label + " Unicode multiline paste", error is null && Normalize(actual.GetProperty("text").GetString()!) == Normalize(value), error ?? "Fixture received: " + actual.GetProperty("text").GetString());
                    Check(label + " no automatic submission", actual.GetProperty("enters").GetInt32() == 0);
                    await Command("set", field, "Alpha Beta", 6, 4); await Task.Delay(100); target = CaptureOwned();
                    error = await Desktop.Paste(target, "café", false); actual = await Command("get", field);
                    Check(label + " selected text replacement", error is null && actual.GetProperty("text").GetString() == "Alpha café", error);
                    await Command("set", field, "Unchanged", 4); await Task.Delay(100); target = CaptureOwned();
                    await Command("select", field, start: 0); await Task.Delay(100);
                    if (target.Caret is not null) Check(label + " moved caret guarded", Desktop.CheckTarget(target) is not null);
                    else results.Add(new { name = label + " moved caret guarded", skipped = true, detail = "UI Automation does not expose a caret anchor for this control." });
                    await Command("set", field, "Before", 6); await Task.Delay(100); target = CaptureOwned();
                    await Command("set", field, "Edited", 6); await Task.Delay(100);
                    Check(label + " edits guarded", Desktop.CheckTarget(target) is not null);
                    target = CaptureOwned(); await Command("focus", field == "plain" ? "rich" : "plain"); await Task.Delay(100);
                    Check(label + " changed field guarded", Desktop.CheckTarget(target) is not null);
                    if (web)
                    {
                        await Command("set", field, "Draft", 5); await Task.Delay(100); target = CaptureOwned();
                        await Command("replace", field, "Draft", 5); await Task.Delay(100);
                        var recreated = CaptureOwned();
                        Check(label + " replacement really changes runtime identity", recreated.Field != target.Field);
                        error = await Desktop.Paste(target, " retained", false); actual = await Command("get", field);
                        Check(label + " recreated editor accepts safe insertion", error is null && actual.GetProperty("text").GetString() == "Draft retained", error);
                    }
                    await Command("set", field, "Before", 6); await Task.Delay(100); target = CaptureOwned();
                    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                    bool canceled = false; try { await Desktop.Paste(target, "SHOULD NOT INSERT", false, cancelled.Token); } catch (OperationCanceledException) { canceled = true; }
                    actual = await Command("get", field); Check(label + " cancellation prevents paste", canceled && actual.GetProperty("text").GetString() == "Before");
                }
                await Command("set", "plain", "", 0); await Task.Delay(100);
                var beforeText = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                string? pasteError = await Desktop.Paste(CaptureOwned(), "clipboard preservation", false);
                Check((web ? "WebView2" : "native") + " original clipboard text preserved", pasteError is null && beforeText == (Clipboard.ContainsText() ? Clipboard.GetText() : null));
                await Command("set", "plain", "", 0); await Task.Delay(100);
                var concurrentPaste = Desktop.Paste(CaptureOwned(), "concurrent clipboard check", false);
                await Task.Delay(120); Clipboard.SetText("NEWER CLIPBOARD CONTENT"); await concurrentPaste;
                Check((web ? "WebView2" : "native") + " newer clipboard preserved", Clipboard.GetText() == "NEWER CLIPBOARD CONTENT");
                if (beforeText is not null) Clipboard.SetText(beforeText); else Clipboard.Clear();
                var overlay = new RecordingOverlay(); var overlayForeground = Desktop.Capture().Window;
                overlay.UpdateState("processing", "Thinking…"); overlay.Show(); await Task.Delay(150);
                Check((web ? "WebView2" : "native") + " overlay does not activate", Desktop.Capture().Window == overlayForeground);
                overlay.Hide(); Check((web ? "WebView2" : "native") + " overlay hides", !overlay.Visible); overlay.Dispose();
                var closed = CaptureOwned(); await Command("close"); await child.WaitForExitAsync();
                Check((web ? "WebView2" : "native") + " closed target guarded", Desktop.CheckTarget(closed) is not null);
            }
            finally { if (!child.HasExited) child.Kill(true); }
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { failures, results }, new JsonSerializerOptions { WriteIndented = true })); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    private static string Normalize(string s) => s.Replace("\r\n", "\n");
    private sealed class Fixture : Form
    {
        private readonly TextBox plain = new() { Name = "plain", AccessibleName = "Plain verification field", Multiline = true, Dock = DockStyle.Top, Height = 150 };
        private readonly RichTextBox rich = new() { Name = "rich", AccessibleName = "Rich verification field", Dock = DockStyle.Fill };
        private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
        private readonly bool useWeb; private int enters;
        public Fixture(bool web)
        {
            useWeb = web; Text = web ? "Murmur browser insertion checks" : "Murmur native insertion checks"; Width = 650; Height = 420; StartPosition = FormStartPosition.CenterScreen;
            if (web) Controls.Add(browser); else { Controls.Add(rich); Controls.Add(plain); }
            plain.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) enters++; }; rich.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) enters++; };
            Shown += async (_, _) =>
            {
                if (web)
                {
                    var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "Murmur-integration-webview"), new CoreWebView2EnvironmentOptions("--force-renderer-accessibility"));
                    await browser.EnsureCoreWebView2Async(env);
                    var ready = new TaskCompletionSource(); browser.NavigationCompleted += (_, _) => ready.TrySetResult();
                    browser.NavigateToString("<html><body><h2>Murmur insertion verification</h2><textarea id='plain' aria-label='Plain verification field' style='width:90%;height:100px'></textarea><div id='rich' contenteditable='true' role='textbox' aria-label='Rich verification field' style='border:1px solid;padding:20px;white-space:pre-wrap'></div><script>window.enters=0;document.addEventListener('keydown',e=>{if(e.key==='Enter')window.enters++});</script></body></html>"); await ready.Task;
                }
                Console.WriteLine("ready"); _ = Task.Run(ReadCommands);
            };
        }
        private async Task ReadCommands()
        {
            while (await Console.In.ReadLineAsync() is { } line)
            {
                using var doc = JsonDocument.Parse(line); var request = doc.RootElement.Clone(); var done = new TaskCompletionSource();
                BeginInvoke(async () => { try { Console.WriteLine(await Act(request)); } catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { error = ex.Message })); } finally { done.TrySetResult(); } });
                await done.Task;
            }
        }
        private async Task<string> Act(JsonElement request)
        {
            string op = request.GetProperty("op").GetString()!, field = request.GetProperty("field").GetString()!, text = request.GetProperty("text").GetString()!;
            int start = request.GetProperty("start").GetInt32(), length = request.GetProperty("length").GetInt32();
            if (op == "close") { BeginInvoke(Close); return "{}"; }
            if (useWeb)
            {
                if (op != "get") { Activate(); browser.Focus(); }
                string script = "(()=>{let el=document.getElementById(" + JsonSerializer.Serialize(field) + ");";
                if (op == "get") script += "return {text:el.tagName==='TEXTAREA'?el.value:el.innerText,enters:window.enters};";
                else
                {
                    if (op == "replace") script += "const next=el.cloneNode(true);el.replaceWith(next);el=next;";
                    if (op is "set" or "replace") script += "if(el.tagName==='TEXTAREA')el.value=" + JsonSerializer.Serialize(text) + ";else el.textContent=" + JsonSerializer.Serialize(text) + ";window.enters=0;";
                    script += "el.focus();";
                    if (op is "set" or "select" or "replace") script += "if(el.tagName==='TEXTAREA')el.setSelectionRange(" + start + "," + (start + length) + ");else{const range=document.createRange();if(el.firstChild){range.setStart(el.firstChild," + start + ");range.setEnd(el.firstChild," + (start + length) + ");}else range.selectNodeContents(el);const selection=getSelection();selection.removeAllRanges();selection.addRange(range);}";
                    script += "return {};";
                }
                return await browser.ExecuteScriptAsync(script + "})()");
            }
            TextBoxBase control = field == "plain" ? plain : rich;
            if (op == "get") return JsonSerializer.Serialize(new { text = control.Text, enters });
            Activate(); control.Focus();
            if (op == "set") { control.Text = text; enters = 0; }
            if (op is "set" or "select") control.Select(start, length);
            return "{}";
        }
    }
}
