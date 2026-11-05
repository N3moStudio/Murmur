import AppKit
import MurmurCore

@MainActor final class Hotkey {
    var action: ((CapsGesture.Action) -> Void)?
    var cancel: (() -> Void)?
    var recording = false
    var busy = false
    private var gesture = CapsGesture()
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?

    func start() throws {
        guard tap == nil else { return }
        let mask = (CGEventMask(1) << CGEventType.flagsChanged.rawValue) | (CGEventMask(1) << CGEventType.keyDown.rawValue)
        let callback: CGEventTapCallBack = { _, type, event, context in
            guard let context else { return Unmanaged.passUnretained(event) }
            // This tap's source is installed exclusively on the main run loop.
            return MainActor.assumeIsolated {
                Unmanaged<Hotkey>.fromOpaque(context).takeUnretainedValue().receive(type, event)
            }
        }
        guard let tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap,
            options: .defaultTap, eventsOfInterest: mask, callback: callback,
            userInfo: Unmanaged.passUnretained(self).toOpaque()) else {
            throw MurmurError.message("Enable Accessibility and Input Monitoring for Murmur in System Settings, then enable the shortcut again.")
        }
        self.tap = tap
        source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
    }
    private func receive(_ type: CGEventType, _ event: CGEvent) -> Unmanaged<CGEvent>? {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
            gesture.reset(); return Unmanaged.passUnretained(event)
        }
        let code = event.getIntegerValueField(.keyboardEventKeycode)
        if type == .flagsChanged && code == 57 {
            let otherModifiers: CGEventFlags = [.maskCommand, .maskControl, .maskAlternate, .maskShift]
            if !event.flags.intersection(otherModifiers).isEmpty {
                gesture.reset(); return Unmanaged.passUnretained(event)
            }
            if busy { gesture.reset(); return nil }
            if let result = gesture.tap(at: ProcessInfo.processInfo.systemUptime, recording: recording) {
                // Return from the event-tap callback before any cross-app AX queries.
                Task { [weak self] in self?.action?(result) }
            }
            return nil
        }
        if type == .keyDown {
            gesture.reset()
            if code == 53 && (recording || busy) {
                Task { [weak self] in self?.cancel?() }
                return nil
            }
        }
        return Unmanaged.passUnretained(event)
    }
    func stop() {
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        if let tap { CGEvent.tapEnable(tap: tap, enable: false); CFMachPortInvalidate(tap) }
        source = nil; tap = nil; gesture.reset()
    }
}
