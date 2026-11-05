using System.Text.RegularExpressions;
namespace Murmur.Core;
public static class TextRules
{
    public static string NormalizeSpokenTimes(string text) => Regex.Replace(text,
        @"\b(at|around|by|until|after|before|from)\s+(1[0-2]|[1-9])\s+([0-5][0-9])\b",
        "$1 $2:$3", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public static bool IsCompleteShortReply(string text)
    {
        if (text.Any(char.IsDigit)) return false;
        string words = Regex.Replace(text.ToLowerInvariant(), @"[^a-z'\s]", "");
        words = Regex.Replace(words.Trim(), @"\s+", " ");
        return words is "yes" or "yes please" or "no" or "no thanks" or "no not yet" or "thank you" or "thanks" or "okay" or "ok" or "sounds good" or "see you soon" or "yes that's correct";
    }
    public static string EmailParagraphs(string text)
    {
        text = Regex.Replace(text, @"^(?<greeting>(?i:hi|hello|hey|dear) [^,\r\n.!?]{1,60},)[ \t]+(?<first>\p{L})",
            m => m.Groups["greeting"].Value + "\n\n" + m.Groups["first"].Value.ToUpperInvariant());
        var signoff = Regex.Match(text, @"[,.]\s+(?<sign>(?i:thanks|thank you|best|regards|sincerely)),\s*(?<name>\p{Lu}[\p{L}'’-]*(?:[ ]\p{Lu}[\p{L}'’-]*){0,2})[.!]?\s*$");
        if (signoff.Success)
        {
            string prefix = text[..signoff.Index].TrimEnd();
            if (prefix.Length > 0 && !".!?".Contains(prefix[^1])) prefix += ".";
            string sign = signoff.Groups["sign"].Value;
            text = prefix + "\n\n" + char.ToUpperInvariant(sign[0]) + sign[1..] + ",\n" + signoff.Groups["name"].Value;
        }
        return text;
    }
    public static string Expand(string text, IEnumerable<LibraryEntry> entries)
    {
        var map = entries.Where(e => !string.IsNullOrWhiteSpace(e.Trigger)).OrderBy(e => e.Shared)
            .GroupBy(e => e.Trigger.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
        if (map.Count == 0) return text;
        var pattern = @"(?<![\p{L}\p{N}_])(?:" + string.Join("|", map.Keys.OrderByDescending(s => s.Length).Select(Regex.Escape)) + @")(?![\p{L}\p{N}_])";
        return Regex.Replace(text, pattern, m => map[m.Value], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
    public static bool PreservesProtected(string original, string changed, IEnumerable<string> vocabulary)
    {
        // Formatting must not add greetings or turn a prohibition into permission.
        string[] invariants = [@"\b(?:thanks|thank you)\b", @"\b(?:hi|hey|hello)\b",
            @"\b(?:best regards|kind regards|sincerely)\b", @"\b(?:no|not|never|without|cannot|[a-z]+n['’]t)\b"];
        foreach (var invariant in invariants)
            if (Regex.Matches(original, invariant, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count !=
                Regex.Matches(changed, invariant, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count) return false;
        const string pattern = @"https?://[^\s<>]+|[\w.+%-]+@[\w.-]+\.[\p{L}]{2,}|\b\d[\d.,:/-]*(?i:st|nd|rd|th)?\b|`[^`]+`|\b[\p{L}]+_[\w]+\b";
        static string Token(Match m) { var token = m.Value.TrimEnd('.', ',', ';', '!', '?'); return char.IsDigit(token[0]) ? Regex.Replace(token, @"(?i)(st|nd|rd|th)$", "") : token; }
        var matches = Regex.Matches(original, pattern);
        var before = matches.Select(Token).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        var after = Regex.Matches(changed, pattern).Select(Token).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        var required = new Dictionary<string, int>(before);
        // Only this unambiguous numeric correction permits dropping an earlier
        // fact. Other dates, amounts, repeated numbers and replacement values stay
        // protected. This is a validation allowance, not an inferred rewrite.
        foreach (Match correction in Regex.Matches(original, @"\bactually\s*,?\s*make\s+that\s+\d[\d.,:/-]*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var previous = matches.LastOrDefault(m => m.Index + m.Length <= correction.Index && char.IsDigit(m.Value[0]));
            if (previous is not null && correction.Index - previous.Index < 200)
            {
                string token = Token(previous);
                if (required[token] > 0) required[token]--;
            }
        }
        if (required.Any(t => after.GetValueOrDefault(t.Key) < t.Value)) return false;
        if (after.Any(t => t.Value > before.GetValueOrDefault(t.Key))) return false;
        return vocabulary.Where(v => v.Length > 0 && original.Contains(v, StringComparison.Ordinal)).All(v => changed.Contains(v, StringComparison.Ordinal));
    }
    public static string Validate(LibraryEntry entry)
    {
        if (!Guid.TryParse(entry.Id, out _)) return "Invalid entry identifier.";
        if (string.IsNullOrWhiteSpace(entry.Trigger) || !entry.Trigger.Any(char.IsLetterOrDigit)) return "Enter a word or phrase.";
        if (entry.Trigger.Length > 60) return "Keep the phrase within 60 characters.";
        if (string.IsNullOrWhiteSpace(entry.Value)) return "Enter the replacement text.";
        if (entry.Value.Length > (entry.Kind == "dictionary" ? 60 : 4000)) return "The replacement is too long.";
        if (entry.Kind is not ("dictionary" or "snippet")) return "Unknown library type.";
        return "";
    }
    public static string Architecture(ushort nativeMachine) => nativeMachine switch { 0xaa64 => "arm64", 0x8664 => "x64", _ => throw new PlatformNotSupportedException("Murmur requires Windows ARM64 or x64.") };
}
