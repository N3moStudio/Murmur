import Foundation

public enum WritingRules {
    public static func format(_ text: String, email: Bool) -> String {
        let source = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard email else { return source }
        var result = source.replacingOccurrences(
            of: #"^(Hi|Hello|Hey|Dear) ([^,\n.!?]{1,60}),[ \t]+"#,
            with: "$1 $2,\n\n", options: [.regularExpression, .caseInsensitive])
        let expression = try! NSRegularExpression(
            pattern: #"[,.]\s+(thanks|thank you|best|regards|sincerely),\s*([\p{L}'’ -]{1,60})[.!]?\s*$"#,
            options: .caseInsensitive)
        let range = NSRange(result.startIndex..., in: result)
        if let match = expression.firstMatch(in: result, range: range),
           let full = Range(match.range, in: result),
           let sign = Range(match.range(at: 1), in: result), let name = Range(match.range(at: 2), in: result) {
            let prefix = String(result[..<full.lowerBound]).trimmingCharacters(in: .whitespacesAndNewlines)
            let closing = String(result[sign]); let person = String(result[name]).trimmingCharacters(in: .whitespacesAndNewlines)
            result = prefix + (prefix.last.map { ".!?".contains($0) } == true ? "" : ".") + "\n\n" + closing.prefix(1).uppercased() + closing.dropFirst() + ",\n" + person
        }
        return result
    }
}
