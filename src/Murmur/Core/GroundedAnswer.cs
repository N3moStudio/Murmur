using System.Text.Json;
using System.Text.RegularExpressions;

namespace Murmur.Core;

public static class GroundedAnswer
{
    public const string Missing = "I couldn't find that information in the saved meeting excerpts.";
    private static readonly HashSet<string> StopWords = new("a an and are as at be been can could did do does for from had has have how i in is it me meeting meetings my notes of on or our please said say that the their them there these they this to us was we were what when where which who why will with would you".Split(' '));
    public static string[] Terms(string question) => Regex.Matches(question.ToLowerInvariant(), @"[\p{L}\d]+").Select(m => m.Value).Where(t => t.Length > 2 && !StopWords.Contains(t)).Distinct().ToArray();
    private static string Normalize(string value) => Regex.Replace(value.Trim(), @"\s+", " ");
    public static string Validate(string generated, IReadOnlyList<string> sources)
    {
        try
        {
            generated = Regex.Replace(generated.Trim(), @"^```(?:json)?\s*|\s*```$", "", RegexOptions.IgnoreCase);
            using var data = JsonDocument.Parse(generated);
            var accepted = new List<string>();
            foreach (var item in data.RootElement.GetProperty("quotes").EnumerateArray().Take(6))
            {
                int source = item.GetProperty("source").GetInt32(); string quote = Normalize(item.GetProperty("quote").GetString() ?? "");
                if (source < 1 || source > sources.Count || quote.Length is < 8 or > 1600 || !Normalize(sources[source - 1]).Contains(quote, StringComparison.Ordinal)) return Missing;
                accepted.Add($"[{source}] “{quote}”");
            }
            return accepted.Count > 0 ? string.Join("\n\n", accepted.Distinct()) : Missing;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return Missing; }
    }
}
