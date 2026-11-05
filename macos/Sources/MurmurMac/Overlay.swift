import AppKit
import MurmurCore

@MainActor final class Overlay {
    private let panel: NSPanel
    private let orb = OrbView(frame: NSRect(x: 0, y: 0, width: 28, height: 28))
    private let label = NSTextField(labelWithString: "")
    private var timer: Timer?
    private var began = 0.0
    var animate = true

    init() {
        panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 210, height: 48),
            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .ignoresCycle]
        panel.isOpaque = false; panel.backgroundColor = .clear
        panel.hasShadow = true; panel.hidesOnDeactivate = false
        panel.ignoresMouseEvents = true; panel.isReleasedWhenClosed = false
        let surface = NSVisualEffectView(frame: panel.contentView!.bounds)
        surface.autoresizingMask = [.width, .height]
        surface.material = .hudWindow; surface.blendingMode = .behindWindow; surface.state = .active
        surface.wantsLayer = true; surface.layer?.cornerRadius = 24
        panel.contentView = surface
        let row = NSStackView(views: [orb, label])
        row.orientation = .horizontal; row.alignment = .centerY; row.spacing = 10
        row.translatesAutoresizingMaskIntoConstraints = false
        label.font = .systemFont(ofSize: 13, weight: .medium)
        label.textColor = .labelColor
        surface.addSubview(row)
        NSLayoutConstraint.activate([
            row.centerXAnchor.constraint(equalTo: surface.centerXAnchor),
            row.centerYAnchor.constraint(equalTo: surface.centerYAnchor),
            orb.widthAnchor.constraint(equalToConstant: 28), orb.heightAnchor.constraint(equalToConstant: 28)
        ])
    }
    func show(_ message: String) {
        label.stringValue = message
        if !panel.isVisible {
            let point = NSEvent.mouseLocation
            let screen = NSScreen.screens.first { $0.frame.contains(point) } ?? NSScreen.main
            if let frame = screen?.visibleFrame {
                panel.setFrameOrigin(NSPoint(x: frame.midX - panel.frame.width / 2, y: frame.minY + 20))
            }
            began = ProcessInfo.processInfo.systemUptime
            panel.orderFrontRegardless()
        }
        timer?.invalidate(); timer = nil
        orb.seconds = 0; orb.needsDisplay = true
        guard animate, !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion else { return }
        let interval = ProcessInfo.processInfo.isLowPowerModeEnabled ? 1.0 / 15 : 1.0 / 30
        let timer = Timer(timeInterval: interval, repeats: true) { [weak self] _ in
            // Scheduled only on the main run loop, and only while visible.
            MainActor.assumeIsolated {
                guard let self else { return }
                self.orb.seconds = ProcessInfo.processInfo.systemUptime - self.began
                self.orb.needsDisplay = true
            }
        }
        self.timer = timer
        RunLoop.main.add(timer, forMode: .common)
    }
    func hide() { timer?.invalidate(); timer = nil; panel.orderOut(nil) }
}

@MainActor private final class OrbView: NSView {
    var seconds = 0.0
    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        let scale = min(bounds.width, bounds.height) / 20
        let isDark = effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
        for dot in ThinkingOrb.frame(seconds: seconds) {
            let white = isDark ? 1 - dot.white : dot.white
            NSColor(white: white, alpha: dot.alpha).setFill()
            NSBezierPath(ovalIn: NSRect(x: (dot.x - dot.radius) * scale,
                y: (20 - dot.y - dot.radius) * scale,
                width: dot.radius * 2 * scale, height: dot.radius * 2 * scale)).fill()
        }
    }
}
