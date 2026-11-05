using System.Text.RegularExpressions;

namespace Murmur.Core;

public static class CleanupSections
{
    // Bound each generation without ever dropping the tail. Exact section text
    // is the cache key, so a revised recognition result invalidates that section.
    public static IReadOnlyList<string> Split(string text, int limit = 900)
    {
        if (limit < 256) throw new ArgumentOutOfRangeException(nameof(limit));
        var result = new List<string>(); int start = 0;
        var corrections = Regex.Matches(text, @"\bactually\s*,?\s*make\s+that\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        while (text.Length - start > limit)
        {
            int boundary = start + limit;
            var sentence = Regex.Matches(text.Substring(start, limit), @"(?<=[.!?])\s+").LastOrDefault(m => m.Index >= limit / 2);
            if (sentence is not null) boundary = start + sentence.Index + sentence.Length;
            else
            {
                while (boundary > start && !char.IsWhiteSpace(text[boundary - 1])) boundary--;
                if (boundary == start) { boundary = start + limit; while (boundary < text.Length && !char.IsWhiteSpace(text[boundary - 1])) boundary++; }
            }
            // Keep an explicit correction with the nearby fact it replaces.
            foreach (Match correction in corrections)
                if (Math.Abs(correction.Index - boundary) < 200 && correction.Index - 200 > start + 256)
                {
                    boundary = correction.Index - 200;
                    while (boundary > start && !char.IsWhiteSpace(text[boundary - 1])) boundary--;
                    break;
                }
            result.Add(text[start..boundary]); start = boundary;
        }
        if (start < text.Length) result.Add(text[start..]);
        return result;
    }
}
