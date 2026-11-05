import AppKit
import MurmurCore

@main @MainActor enum MurmurMain {
    static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)
        let delegate = AppDelegate()
        app.delegate = delegate
        app.run()
        withExtendedLifetime(delegate) {}
    }
}

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate {
    private enum Phase { case idle, starting, recording, finishing, cancelling }
    private let speech = SpeechSession()
    private let history = HistoryStore()
    private let overlay = Overlay()
    private let hotkey = Hotkey()
    private var phase = Phase.idle
    private var generation = UUID()
    private var stopped: Destination?
    private var item: NSStatusItem!
    private var statusItem: NSMenuItem!
    private var setupWindow: NSWindow?
    private var statusLabel: NSTextField?
    private var setupButton: NSButton?
    private var resultWindow: NSWindow?
    private var resultView: NSTextView?
    private var latestText = ""
    private var setupRunning = false
    private var recordingTask: Task<Void, Never>?
    private var setupComplete: Bool { UserDefaults.standard.bool(forKey: "englishSetupComplete") }

    func applicationDidFinishLaunching(_ notification: Notification) {
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        item.button?.image = NSImage(systemSymbolName: "waveform", accessibilityDescription: "Murmur")
        let menu = NSMenu()
        statusItem = NSMenuItem(title: "Murmur • Ready", action: nil, keyEquivalent: "")
        menu.addItem(statusItem)
        add(menu, "Set up Murmur…", #selector(showSetup))
        add(menu, "Last transcript…", #selector(showTranscript))
        add(menu, "Open saved dictations", #selector(openHistory))
        menu.addItem(.separator())
        add(menu, "Quit Murmur", #selector(quit))
        item.menu = menu
        overlay.animate = !UserDefaults.standard.bool(forKey: "disableOrbMotion")
        hotkey.action = { [weak self] action in
            switch action { case .start: self?.start(); case .stop: self?.stop() }
        }
        hotkey.cancel = { [weak self] in self?.cancel() }
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(willSleep),
            name: NSWorkspace.willSleepNotification, object: nil)
        if setupComplete && Permissions.accessibility { enableShortcut() }
        else { showSetup() }
    }
    private func add(_ menu: NSMenu, _ title: String, _ action: Selector) {
        let entry = NSMenuItem(title: title, action: action, keyEquivalent: "")
        entry.target = self; menu.addItem(entry)
    }
    private func status(_ text: String) {
        statusItem.title = "Murmur • " + text
        statusLabel?.stringValue = text
    }
    @objc private func showSetup() {
        if setupWindow == nil {
            let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 540, height: 600),
                styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
            window.title = "Murmur setup"; window.isReleasedWhenClosed = false
            window.contentMinSize = NSSize(width: 540, height: 600)
            let title = NSTextField(labelWithString: "Your words, right where you need them.")
            title.font = .systemFont(ofSize: 20, weight: .semibold)
            let description = NSTextField(wrappingLabelWithString:
                "Double-tap Caps Lock to start. Tap once to finish. Escape cancels. English speech stays on this Mac. The small orb appears only during dictation.")
            let prepare = NSButton(title: "↓ Download English & enable microphone", target: self, action: #selector(prepare))
            setupButton = prepare
            let access = NSButton(title: "Allow Accessibility", target: self, action: #selector(allowAccessibility))
            let enable = NSButton(title: "Enable Caps Lock shortcut", target: self, action: #selector(enableShortcut))
            let motion = NSButton(checkboxWithTitle: "Animate the thinking orb", target: self, action: #selector(toggleMotion(_:)))
            motion.state = overlay.animate ? .on : .off
            let detail = NSTextField(wrappingLabelWithString:
                "Select a text field before finishing. If safe insertion is unavailable, your transcript and recording stay in saved dictations. This is an early Mac preview.")
            detail.textColor = .secondaryLabelColor
            let state = NSTextField(wrappingLabelWithString: setupComplete ? "English setup completed. Enable the shortcut when permissions are ready." : "Start with the English download.")
            statusLabel = state
            let stack = NSStackView(views: [title, description, prepare, access, enable, motion, detail, state])
            stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 16
            stack.translatesAutoresizingMaskIntoConstraints = false
            window.contentView!.addSubview(stack)
            NSLayoutConstraint.activate([
                stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 24),
                stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -24),
                stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 24),
                stack.bottomAnchor.constraint(lessThanOrEqualTo: window.contentView!.bottomAnchor, constant: -20)
            ])
            window.center(); setupWindow = window
        }
        NSApp.activate(ignoringOtherApps: true); setupWindow?.makeKeyAndOrderFront(nil)
    }
    @objc private func prepare() {
        guard !setupRunning, phase == .idle else { return }
        setupRunning = true; setupButton?.isEnabled = false
        status("Preparing English speech…")
        Task {
            defer { setupRunning = false; setupButton?.isEnabled = true }
            guard await Permissions.microphone() else { status("Allow microphone access in System Settings, then retry."); return }
            do {
                try await speech.prepareEnglish()
                UserDefaults.standard.set(true, forKey: "englishSetupComplete")
                status("English is ready. Allow Accessibility, then enable the shortcut.")
            } catch { status(error.localizedDescription) }
        }
    }
    @objc private func allowAccessibility() { Permissions.requestAccessibility() }
    @objc private func enableShortcut() {
        guard setupComplete else { status("Download English speech first."); return }
        guard Permissions.accessibility else { status("Allow Accessibility, then enable the shortcut again."); return }
        do { try hotkey.start(); status("Ready • double-tap Caps Lock") }
        catch { status(error.localizedDescription) }
    }
    @objc private func toggleMotion(_ sender: NSButton) {
        overlay.animate = sender.state == .on
        UserDefaults.standard.set(!overlay.animate, forKey: "disableOrbMotion")
    }
    private func start() {
        guard phase == .idle, !setupRunning, setupComplete else { return }
        let token = UUID(); generation = token; stopped = nil
        phase = .starting; hotkey.recording = true; hotkey.busy = false
        overlay.show("Starting…"); status("Starting microphone…")
        recordingTask = Task { await record(token) }
    }
    private func record(_ token: UUID) async {
        var audioURL: URL?
        var candidateText = ""
        var attemptedInsert = false
        var sessionID = UUID()
        do {
            guard await Permissions.microphone() else { throw MurmurError.message("Microphone access is disabled.") }
            guard token == generation else { return }
            let url = try await history.recordingURL(); audioURL = url
            guard token == generation else { return }
            try await speech.start(recoveryURL: url)
            guard token == generation else { await speech.cancel(); return }
            if phase == .finishing { await speech.finishInput() }
            else { phase = .recording; overlay.show("Listening…"); status("Listening • Caps Lock to finish") }
            let raw = try await speech.waitForResult()
            guard token == generation else { return }
            guard !raw.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
                throw MurmurError.message("No speech detected. The recording is saved for recovery.")
            }
            let destination = Destination.resolve(stopped: stopped)
            let text = WritingRules.format(raw, email: destination?.email ?? stopped?.email ?? false)
            latestText = text; candidateText = text
            sessionID = UUID()
            var saved = HistoryItem(id: sessionID, date: Date(), text: text, inserted: false,
                audioFile: url.lastPathComponent, error: nil)
            // Persist before attempting an external write. Never auto-retry an unconfirmed insertion.
            try await history.save(saved)
            guard token == generation else { return }
            attemptedInsert = destination != nil
            let inserted = destination?.insert(text) ?? false
            saved.inserted = inserted
            saved.error = inserted ? nil : "Insertion was unavailable or unconfirmed. Check the destination before copying this transcript."
            try await history.save(saved)
            if inserted { try? await history.removeAudio(url) }
            status(inserted ? "Inserted • ready" : "Transcript saved • open Last transcript")
        } catch {
            await speech.cancel()
            guard token == generation else { return }
            let detail = error.localizedDescription + (attemptedInsert ? " Check the destination before copying: insertion may already have occurred." : "")
            let saved = HistoryItem(id: sessionID, date: Date(), text: candidateText, inserted: false,
                audioFile: audioURL?.lastPathComponent, error: detail)
            do { try await history.save(saved); status(detail) }
            catch { status(detail + " Recovery note could not be saved; any audio remains in saved dictations.") }
        }
        guard token == generation else { return }
        reset()
    }
    private func stop() {
        guard phase == .recording || phase == .starting else { return }
        stopped = Destination.capture()
        phase = .finishing; hotkey.recording = false; hotkey.busy = true
        overlay.show("Finishing…"); status("Finishing dictation…")
        Task { await speech.finishInput() }
    }
    private func cancel() {
        guard phase != .idle, phase != .cancelling else { return }
        generation = UUID(); phase = .cancelling
        hotkey.recording = false; hotkey.busy = true; overlay.hide()
        let pending = recordingTask
        Task {
            await speech.cancel()
            await pending?.value
            recordingTask = nil
            reset(); status("Cancelled • any captured audio stays in saved dictations")
        }
    }
    private func reset() {
        recordingTask = nil; phase = .idle; hotkey.recording = false; hotkey.busy = false; stopped = nil; overlay.hide()
    }
    @objc private func willSleep() { cancel() }
    @objc private func showTranscript() {
        if resultWindow == nil {
            let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 640, height: 420),
                styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
            window.title = "Last transcript"; window.isReleasedWhenClosed = false
            let scroll = NSScrollView(frame: window.contentView!.bounds)
            scroll.autoresizingMask = [.width, .height]; scroll.hasVerticalScroller = true
            let text = NSTextView(frame: scroll.bounds)
            text.isEditable = false; text.isSelectable = true; text.isRichText = false
            text.font = .systemFont(ofSize: 16); text.textContainerInset = NSSize(width: 20, height: 20)
            text.autoresizingMask = [.width]; text.isVerticallyResizable = true
            text.textContainer?.widthTracksTextView = true
            scroll.documentView = text; window.contentView = scroll
            resultWindow = window; resultView = text; window.center()
        }
        resultView?.string = latestText.isEmpty ? "No transcript in this session. Previous dictations are in the saved dictations folder." : latestText
        NSApp.activate(ignoringOtherApps: true); resultWindow?.makeKeyAndOrderFront(nil)
    }
    @objc private func openHistory() {
        let root = history.root
        do { try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700]) }
        catch { status(error.localizedDescription); return }
        NSWorkspace.shared.open(root)
    }
    @objc private func quit() { NSApp.terminate(nil) }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        generation = UUID(); hotkey.stop(); overlay.hide()
        Task {
            await speech.cancel()
            sender.reply(toApplicationShouldTerminate: true)
        }
        return .terminateLater
    }
    func applicationWillTerminate(_ notification: Notification) {
        hotkey.stop(); overlay.hide()
        NSWorkspace.shared.notificationCenter.removeObserver(self)
    }
}
