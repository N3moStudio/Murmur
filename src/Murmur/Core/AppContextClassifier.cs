using System.Text.RegularExpressions;

namespace Murmur.Core;

// Adapted from OpenWhispr toneAppCategories.js (MIT © 2024 OpenWhispr Team).
// Only foreground process/title metadata is considered. No background window scan.
public static partial class AppContextClassifier
{
    private static bool Token(string value, string token) => Regex.IsMatch(value, "(?:^|[^a-z0-9])" + Regex.Escape(token) + "(?:$|[^a-z0-9])", RegexOptions.CultureInvariant);
    private static bool BrowserName(string value) => Regex.IsMatch(value, @"^(?:google chrome|chromium|microsoft edge|mozilla firefox|firefox|librewolf|waterfox|floorp|zen browser|brave|opera|vivaldi|thorium|yandex)(?: private browsing| \(.*\))?$", RegexOptions.CultureInvariant);
    private static IEnumerable<(string Name, string[] Apps, string[] Sites)> Categories()
    {
        yield return ("email", APP_SIGNATURES_email, SITE_TITLES_email);
        yield return ("work", APP_SIGNATURES_work, SITE_TITLES_work);
        yield return ("personal", APP_SIGNATURES_personal, SITE_TITLES_personal);
    }
    public static string Classify(string process, string title)
    {
        var signature = Regex.Replace(process.ToLowerInvariant(), @"chrome_widgetwin_\d+", " ");
        foreach (var category in Categories()) if (category.Apps.Any(app => Token(signature, app))) return category.Name;
        if (!BROWSER_SIGNATURES.Append("applicationframehost").Any(app => Token(signature, app))) return "other";
        var cleanTitle = Regex.Replace(title.ToLowerInvariant(), "[\u200b-\u200d\ufeff]", "");
        var parts = Regex.Split(cleanTitle, @"\s[-–—|·•/]\s").Select(s => s.Trim()).ToList();
        if (parts.Count > 0 && BrowserName(parts[^1])) parts.RemoveAt(parts.Count - 1);
        else if (parts.Count > 1 && BrowserName(parts[^2])) parts.RemoveRange(parts.Count - 2, 2);
        var candidates = parts.TakeLast(cleanTitle.EndsWith("microsoft edge", StringComparison.Ordinal) ? 2 : 1)
            .Select(s => Regex.Replace(Regex.Replace(s, @"^\(\d+\+?\)\s*", ""), @"\sand \d+ more (?:pages?|tabs?)$", "").Trim()).ToList();
        foreach (var category in Categories()) if (candidates.Any(category.Sites.Contains)) return category.Name;
        return "other";
    }
}
