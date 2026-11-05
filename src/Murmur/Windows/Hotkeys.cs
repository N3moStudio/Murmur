using System.Runtime.InteropServices;
using System.Windows.Forms;
using Murmur.Core;

namespace Murmur.Windows;

public sealed class Hotkeys : IDisposable
{
    private delegate nint HookProc(int code, nint wp, nint lp);
    private readonly HookProc callback;
    private nint hook;
    private readonly HashSet<int> pressed = [];
    private HashSet<int> hold = [], toggle = [], command = [];
    private bool held, toggled, commanded;
    private int doubleKey;
    private ControlDoubleTap doubleTap = new();
    private readonly HashSet<int> suppressed = [];
    public event Action<string>? Action;
    public Func<bool>? IsRecording;
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wp, nint lp);
    [DllImport("kernel32.dll")] private static extern nint GetModuleHandle(string? module);
    public Hotkeys() { callback = Handle; hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0); if (hook == 0) throw new InvalidOperationException("Windows could not register the recording shortcut."); }
    public void Configure(string holdText, string toggleText, string commandText)
    {
        int tapKey = toggleText.Trim().ToLowerInvariant() switch { "double ctrl" => 0x11, "double caps lock" or "double capslock" => 0x14, _ => 0 };
        bool useDouble = tapKey != 0;
        var a = Parse(holdText); var b = useDouble ? new HashSet<int>() : Parse(toggleText); var c = Parse(commandText);
        if (a.SetEquals(b) || a.SetEquals(c) || b.SetEquals(c)) throw new ArgumentException("Each recording shortcut must be different.");
        if (tapKey == 0x14 && (a.SetEquals([0x14]) || c.SetEquals([0x14]))) throw new ArgumentException("Caps Lock is already used for hands-free dictation.");
        hold = a; toggle = b; command = c; doubleKey = tapKey; doubleTap = new(tapKey); Reset();
    }
    public static HashSet<int> Parse(string value)
    {
        var result = new HashSet<int>();
        foreach (var token in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var key = token.ToLowerInvariant() switch { "ctrl" or "control" => 0x11, "alt" => 0x12, "shift" => 0x10, "win" or "windows" => 0x5b, "space" => 0x20, "capslock" => 0x14, _ => Enum.TryParse<Keys>(token, true, out var parsed) ? (int)parsed : -1 };
            if (key < 0 || !result.Add(key)) throw new ArgumentException("Use unique keys, for example Ctrl+Alt+Space.");
        }
        if (result.Count is 0 or > 3 || (result.Count == 1 && !result.Contains(0x14) && !result.Any(k => k >= 112 && k <= 123))) throw new ArgumentException("Use two or three keys, CapsLock, or an F key.");
        if (result.SetEquals([0x11, 0x12, 0x2e]) || result.SetEquals([0x12, 0x73]) || result.Contains(0x5b) && result.Overlaps([0x4c, 0x44, 0x45, 0x52])) throw new ArgumentException("That shortcut is reserved by Windows.");
        return result;
    }
    public void Reset() { pressed.Clear(); suppressed.Clear(); doubleTap.Reset(); held = toggled = commanded = false; }
    private static int Normalize(int key) => key switch { 0xa0 or 0xa1 => 0x10, 0xa2 or 0xa3 => 0x11, 0xa4 or 0xa5 => 0x12, 0x5c => 0x5b, _ => key };
    private nint Handle(int code, nint wp, nint lp)
    {
        if (code < 0) return CallNextHookEx(hook, code, wp, lp);
        int flags = Marshal.ReadInt32(lp, 8); if ((flags & 0x10) != 0) return CallNextHookEx(hook, code, wp, lp);
        int key = Normalize(Marshal.ReadInt32(lp)); bool down = wp == 0x100 || wp == 0x104; bool up = wp == 0x101 || wp == 0x105;
        if (!down && !up) return CallNextHookEx(hook, code, wp, lp);
        bool repeat = down && pressed.Contains(key); if (down) pressed.Add(key); else pressed.Remove(key);
        if (repeat) return suppressed.Contains(key) ? 1 : CallNextHookEx(hook, code, wp, lp);
        if (down && doubleKey == 0x14 && key == 0x14 && pressed.Count == 1 && IsRecording?.Invoke() != true) Action?.Invoke("prepare");
        if (doubleKey != 0 && doubleTap.Feed(key, down, Environment.TickCount64, pressed.Any(k => k != doubleKey), IsRecording?.Invoke() == true)) Action?.Invoke("toggle");
        if (down && key == 0x1b) Action?.Invoke("cancel");
        bool isToggle = toggle.Count > 0 && toggle.IsSubsetOf(pressed), isCommand = command.Count > 0 && command.IsSubsetOf(pressed);
        bool isHold = hold.Count > 0 && hold.IsSubsetOf(pressed);
        // Dedicated Caps Lock taps must not change letter case. Shift+Caps Lock
        // remains available for normal capitalization and never starts recording.
        bool capsTap = doubleKey == 0x14 && key == 0x14 && !pressed.Any(k => k != 0x14);
        bool consume = down && (isToggle || isCommand || isHold || capsTap);
        if (isCommand && !commanded) { commanded = true; held = false; Action?.Invoke("commandStart"); }
        else if (isToggle && !toggled) { toggled = true; held = false; Action?.Invoke("toggle"); }
        else if (isHold && !held && !toggled && !commanded) { held = true; Action?.Invoke("holdStart"); }
        if (commanded && !isCommand) { commanded = false; Action?.Invoke("holdStop"); }
        else if (held && !isHold) { held = false; Action?.Invoke("holdStop"); }
        if (!isToggle) toggled = false;
        if (down && consume) suppressed.Add(key);
        if (up) consume = suppressed.Remove(key);
        return consume ? 1 : CallNextHookEx(hook, code, wp, lp);
    }
    public void Dispose() { if (hook != 0) UnhookWindowsHookEx(hook); hook = 0; }
}
