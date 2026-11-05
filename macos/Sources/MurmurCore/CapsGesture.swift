import Foundation

public struct CapsGesture: Sendable {
    public enum Action: Sendable, Equatable { case start, stop }
    private var previousTap: TimeInterval?
    public init() {}
    public mutating func reset() { previousTap = nil }
    public mutating func tap(at time: TimeInterval, recording: Bool) -> Action? {
        if recording { previousTap = nil; return .stop }
        if let previousTap, time >= previousTap, time - previousTap <= 0.35 {
            self.previousTap = nil
            return .start
        }
        previousTap = time
        return nil
    }
}
