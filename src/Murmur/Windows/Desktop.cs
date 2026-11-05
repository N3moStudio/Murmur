using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using Murmur.Core;
using Forms = System.Windows.Forms;

namespace Murmur.Windows;

public static class Desktop
{
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hWnd, System.Text.StringBuilder text, int length);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion union; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT key; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public nuint extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData, flags, time; public nuint extra; }
    public static Target CaptureWindow()
    {
        nint window = GetForegroundWindow(); GetWindowThreadProcessId(window, out uint pid);
        string process; try { process = Process.GetProcessById((int)pid).ProcessName; } catch { process = "Unknown"; }
        var title = new System.Text.StringBuilder(1024); GetWindowText(window, title, title.Capacity);
        return new(window, process, "", null, "", false, InteractionRules.Category(process, title.ToString(), ""));
    }
    public static Target Capture(Target? originalWindow = null)
    {
        var original = originalWindow ?? CaptureWindow();
        if (GetForegroundWindow() != original.Window) return original;
        string field = "", selection = "", fieldLabel = ""; string? value = null; bool editable = false; int? caret = null;
        string stableField = "";
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is not null)
            {
                field = string.Join("-", element.GetRuntimeId());
                fieldLabel = element.Current.Name;
                var aid = element.Current.AutomationId;
                // Chromium may recreate the same editor between keystrokes. A
                // fallback identity must still match its label/id, type, bounds
                // AND full text. An anonymous field never gets this fallback.
                if (aid.Length > 0 || fieldLabel.Length > 0)
                {
                    var bounds = element.Current.BoundingRectangle;
                    stableField = Json.Write(new { aid, fieldLabel, type = element.Current.ControlType.Id, bounds.X, bounds.Y, bounds.Width, bounds.Height });
                }
                bool hasValue = element.TryGetCurrentPattern(ValuePattern.Pattern, out var vp);
                if (hasValue) { var v = (ValuePattern)vp; editable = !v.Current.IsReadOnly; if (v.Current.Value.Length <= 200000) value = v.Current.Value; }
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out var tp))
                {
                    var text = (TextPattern)tp; var selections = text.GetSelection(); selection = string.Join("\n", selections.Select(s => s.GetText(40000)));
                    if (!hasValue) editable = text.DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute) is false;
                    if (value is null) { var content = text.DocumentRange.GetText(200001); if (content.Length <= 200000) value = content; }
                    if (selections.Length == 1) { var prefix = text.DocumentRange.Clone(); prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, selections[0], TextPatternRangeEndpoint.Start); int length = prefix.GetText(200001).Length; if (length <= 200000) caret = length; }
                }
                // A web page itself is also a Document. Its type alone does not
                // establish that it is an editor, or override a read-only flag.
                if (element.Current.IsPassword) { editable = false; selection = ""; value = null; }
            }
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException) { }
        if (GetForegroundWindow() != original.Window) return original;
        return original with { Field = field, Value = value, Selection = selection, Editable = editable, Caret = caret, StableField = stableField };
    }
    public static string? CheckTarget(Target target)
    {
        if (!IsWindow(target.Window)) return "The original window closed. Copy the result or focus another field and choose Retry.";
        if (GetForegroundWindow() != target.Window) return "Focus changed while processing. Your text is saved; focus the intended field and choose Retry.";
        var now = Capture();
        if (!target.Editable) return "The original field did not expose editing support. Your result is saved; use Copy or Retry.";
        if (target.Field.Length > 0 && now.Field != target.Field && !InteractionRules.SameRecreatedField(target.StableField, now.StableField, target.Value, now.Value, target.Caret, now.Caret)) return "The selected field changed. Your result is ready to copy or retry.";
        if (target.Value is not null && now.Value != target.Value) return "The field was edited while processing. Your result is saved to avoid replacing your changes.";
        if (target.Selection != now.Selection) return "The text selection changed while processing. Your result is saved; use Copy or Retry.";
        if (target.Caret is not null && target.Caret != now.Caret) return "The cursor moved while processing. Your result is saved; use Copy or Retry.";
        if (!now.Editable) return "No supported editable field is focused. Use Copy, then paste where you want the result.";
        if (target.Process.Equals(Brand.Name, StringComparison.OrdinalIgnoreCase)) return "Your result is saved in History. Focus another app to insert it.";
        return null;
    }
    public static async Task<Target?> ResolveDictationTarget(Target? stopped, CancellationToken ct)
    {
        try { return DictationTarget.AtCompletion(stopped, await Task.Run(() => Capture(), ct).WaitAsync(TimeSpan.FromSeconds(2), ct)); }
        catch (TimeoutException) { return stopped; }
    }
    private static INPUT Key(ushort key, bool up = false) => new() { type = 1, union = new() { key = new() { vk = key, flags = up ? 2u : 0 } } };
    private static bool Chord(ushort key) { INPUT[] keys = [Key(0x11), Key(key), Key(key, true), Key(0x11, true)]; return SendInput((uint)keys.Length, keys, Marshal.SizeOf<INPUT>()) == keys.Length; }
    public static async Task<string?> Paste(Target target, string text, bool submit, CancellationToken ct = default)
    {
        var error = await Task.Run(() => CheckTarget(target), ct); if (error is not null) return error;
        for (int i = 0; i < 40 && new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); i++) await Task.Delay(25, ct);
        if (new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) return "Release the shortcut keys, then retry insertion.";
        Forms.IDataObject? original;
        try
        {
            var source = Forms.Clipboard.GetDataObject(); var copy = new Forms.DataObject();
            if (source is not null) foreach (var format in source.GetFormats(false)) { var value = source.GetData(format, false); if (value is not null) copy.SetData(format, false, value); }
            original = source is null ? null : copy;
        }
        catch (ExternalException) { return "The clipboard is busy. Retry insertion or use Copy."; }
        uint owned = 0;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (await Task.Run(() => CheckTarget(target), ct) is { } changed) return changed;
            Forms.Clipboard.SetText(text); owned = GetClipboardSequenceNumber();
            if (GetForegroundWindow() != target.Window || GetClipboardSequenceNumber() != owned) return "Focus or clipboard changed. Your result is saved.";
            if (!Chord(0x56)) return "Windows blocked insertion. Elevated apps require manual paste; your text is saved.";
            await Task.Delay(350);
            if (submit && !ct.IsCancellationRequested && GetForegroundWindow() == target.Window) { INPUT[] keys = [Key(0x0d), Key(0x0d, true)]; SendInput(2, keys, Marshal.SizeOf<INPUT>()); }
            return null;
        }
        catch (ExternalException) { return "The clipboard is busy. Your result is saved."; }
        finally
        {
            // Never overwrite another application's newer clipboard content.
            if (owned != 0 && GetClipboardSequenceNumber() == owned)
                try { if (original is null) Forms.Clipboard.Clear(); else Forms.Clipboard.SetDataObject(original, true, 3, 30); } catch (ExternalException) { }
        }
    }
    public static async Task<string> CopySelection(Target target)
    {
        if (!string.IsNullOrWhiteSpace(target.Selection)) return target.Selection;
        if (!target.Editable || GetForegroundWindow() != target.Window) throw new InvalidOperationException("Select text in an editable field before using a command.");
        // Require a real selection so paste replaces exactly the text being edited.
        await Task.CompletedTask;
        throw new InvalidOperationException("This application does not expose selected text. Copy it into Scratchpad to transform it.");
    }
}
