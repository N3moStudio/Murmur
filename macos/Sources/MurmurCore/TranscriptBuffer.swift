import Foundation

public struct TranscriptBuffer: Sendable {
    private struct Part: Sendable { let start: Double; let end: Double; let text: String }
    private var parts: [Part] = []
    public init() {}
    public mutating func apply(start: Double, end: Double, text: String) {
        guard start.isFinite, end.isFinite, end >= start else { return }
        parts.removeAll { ($0.start < end && start < $0.end) || $0.start == start }
        if !text.isEmpty { parts.append(Part(start: start, end: end, text: text)) }
    }
    public var text: String {
        parts.sorted { $0.start < $1.start }.map { $0.text.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }.joined(separator: " ")
    }
}
