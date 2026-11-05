namespace Murmur.Core;

public sealed class ControlDoubleTap(int tapKey = 0x11)
{
    private long downAt = -1, lastUp = -1;
    private bool candidate;
    public void Reset() { downAt = lastUp = -1; candidate = false; }
    public bool Feed(int key, bool down, long milliseconds, bool anotherKeyHeld = false, bool singleTapToStop = false)
    {
        if (key != tapKey) { if (down) Reset(); return false; }
        if (down) { candidate = !anotherKeyHeld; downAt = milliseconds; if (!candidate) lastUp = -1; return false; }
        bool tap = candidate && downAt >= 0 && milliseconds - downAt <= 230;
        candidate = false; downAt = -1;
        if (!tap) { lastUp = -1; return false; }
        if (singleTapToStop) { lastUp = -1; return true; }
        if (lastUp >= 0 && milliseconds - lastUp <= 420) { lastUp = -1; return true; }
        lastUp = milliseconds; return false;
    }
}

public static class InteractionRules
{
    public static bool SameRecreatedField(string before, string after, string? beforeText, string? afterText, int? beforeCaret, int? afterCaret)
        => before.Length > 0 && before == after && beforeText is not null && beforeText == afterText
            && beforeCaret is not null && beforeCaret == afterCaret;
    public static bool ShowOverlay(string state) => state is "starting" or "recording" or "processing";
    public static string Category(string process, string windowTitle, string fieldLabel)
    {
        string p = process.ToLowerInvariant();
        var category = AppContextClassifier.Classify(process, windowTitle);
        if (category != "other") return category;
        if (p is "winword" or "onenote" or "notepad" or "notion" || System.Text.RegularExpressions.Regex.IsMatch(windowTitle, @"(?:^|[-–—|]\s)Google Docs(?:\s[-–—|]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "document";
        return "other";
    }
    public static string Formatting(string category) => category switch
    {
        "email" => "For this email, split the spoken greeting, body and spoken sign-off into paragraphs. Do not add a greeting, sign-off, AM/PM or a subject that was not spoken.",
        "personal" => "This is a personal chat message. Keep it natural and compact. Do not add email greetings, a subject or a sign-off.",
        "work" => "This is a workplace message. Keep it concise and readable. Use line breaks or a list only when the spoken content calls for them. Do not add email boilerplate.",
        "document" => "This is a document or note. Use paragraphs and explicit spoken lists. Preserve technical text and code exactly.",
        _ => "Match the spoken structure. Do not add a greeting, subject, sign-off or invented headings."
    };
}
