import AppKit
import AVFoundation

@MainActor enum Permissions {
    static var accessibility: Bool { AXIsProcessTrusted() }
    static func requestAccessibility() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
    }
    static func microphone() async -> Bool { await AVCaptureDevice.requestAccess(for: .audio) }
}

enum MurmurError: LocalizedError {
    case message(String)
    var errorDescription: String? { switch self { case .message(let message): message } }
}
