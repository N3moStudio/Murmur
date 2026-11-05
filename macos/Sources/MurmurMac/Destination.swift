import AppKit

@MainActor struct Destination {
    let element: AXUIElement
    let process: pid_t
    let value: String
    let range: CFRange
    let email: Bool

    static func capture() -> Destination? {
        guard Permissions.accessibility,
              let app = NSWorkspace.shared.frontmostApplication,
              app.processIdentifier != ProcessInfo.processInfo.processIdentifier else { return nil }
        let appElement = AXUIElementCreateApplication(app.processIdentifier)
        AXUIElementSetMessagingTimeout(appElement, 0.15)
        var focused: CFTypeRef?
        guard AXUIElementCopyAttributeValue(appElement, kAXFocusedUIElementAttribute as CFString, &focused) == .success,
              let focused, CFGetTypeID(focused) == AXUIElementGetTypeID() else { return nil }
        let element = focused as! AXUIElement
        AXUIElementSetMessagingTimeout(element, 0.15)
        guard let role = string(element, kAXRoleAttribute), [kAXTextFieldRole, kAXTextAreaRole, kAXComboBoxRole].contains(role),
              string(element, kAXSubroleAttribute) != kAXSecureTextFieldSubrole,
              let value = string(element, kAXValueAttribute), value.utf16.count <= 200_000 else { return nil }
        var writable = DarwinBoolean(false)
        guard AXUIElementIsAttributeSettable(element, kAXSelectedTextAttribute as CFString, &writable) == .success, writable.boolValue else { return nil }
        var rangeObject: CFTypeRef?; var range = CFRange(location: 0, length: 0)
        guard AXUIElementCopyAttributeValue(element, kAXSelectedTextRangeAttribute as CFString, &rangeObject) == .success,
              let rangeObject, CFGetTypeID(rangeObject) == AXValueGetTypeID(),
              AXValueGetValue(rangeObject as! AXValue, .cfRange, &range), range.location >= 0,
              range.length >= 0, range.location + range.length <= value.utf16.count else { return nil }
        var windowObject: CFTypeRef?
        _ = AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute as CFString, &windowObject)
        let title: String
        if let windowObject, CFGetTypeID(windowObject) == AXUIElementGetTypeID() { title = string(windowObject as! AXUIElement, kAXTitleAttribute) ?? "" }
        else { title = "" }
        let context = ((app.bundleIdentifier ?? "") + " " + title).lowercased()
        return Destination(element: element, process: app.processIdentifier, value: value, range: range,
            email: ["com.apple.mail", "outlook", "gmail", "proton mail", "yahoo mail"].contains { context.contains($0) })
    }
    static func resolve(stopped: Destination?) -> Destination? {
        guard let current = capture() else { return nil }
        guard let stopped else { return current }
        guard current.process == stopped.process, CFEqual(current.element, stopped.element), current.value == stopped.value else { return nil }
        return current
    }
    func insert(_ text: String) -> Bool {
        guard let current = Destination.capture(), current.process == process,
              CFEqual(current.element, element), current.value == value,
              current.range.location == range.location, current.range.length == range.length else { return false }
        let expected = (value as NSString).replacingCharacters(in: NSRange(location: range.location, length: range.length), with: text)
        guard AXUIElementSetAttributeValue(element, kAXSelectedTextAttribute as CFString, text as CFString) == .success else { return false }
        // No clipboard or synthetic-key fallback: an unconfirmed write is saved
        // for recovery and is never automatically repeated.
        return Destination.string(element, kAXValueAttribute) == expected
    }
    private static func string(_ element: AXUIElement, _ attribute: String) -> String? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, attribute as CFString, &value) == .success else { return nil }
        return value as? String
    }
}
