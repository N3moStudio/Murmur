import Foundation
import Testing
@testable import MurmurCore

struct CoreTests {
    @Test func capsLockPairStartsAndSingleTapStops() {
        var gesture = CapsGesture()
        #expect(gesture.tap(at: 1, recording: false) == nil)
        #expect(gesture.tap(at: 1.2, recording: false) == .start)
        #expect(gesture.tap(at: 1.25, recording: true) == .stop)
        #expect(gesture.tap(at: 2, recording: false) == nil)
        #expect(gesture.tap(at: 2.3, recording: false) == .start)
    }
    @Test func successfulPairCannotBeReused() {
        var gesture = CapsGesture()
        _ = gesture.tap(at: 1, recording: false)
        #expect(gesture.tap(at: 1.1, recording: false) == .start)
        #expect(gesture.tap(at: 1.2, recording: false) == nil)
    }
    @Test func slowPairAndResetDoNotStart() {
        var gesture = CapsGesture()
        _ = gesture.tap(at: 1, recording: false)
        #expect(gesture.tap(at: 1.5, recording: false) == nil)
        gesture.reset()
        #expect(gesture.tap(at: 1.6, recording: false) == nil)
        #expect(gesture.tap(at: 1.8, recording: false) == .start)
    }
    @Test func correctedPhraseDoesNotRepeatOrLoseTail() {
        var buffer = TranscriptBuffer()
        buffer.apply(start: 0, end: 2, text: "Send thirty invoices.")
        buffer.apply(start: 2, end: 4, text: "Do not cancel.")
        buffer.apply(start: 0, end: 2, text: "Send 38 invoices.")
        #expect(buffer.text == "Send 38 invoices. Do not cancel.")
        buffer.apply(start: 2, end: 4, text: "")
        #expect(buffer.text == "Send 38 invoices.")
    }
    @Test func combinedCorrectionReplacesOldSpans() {
        var buffer = TranscriptBuffer()
        buffer.apply(start: 0, end: 1, text: "Hi")
        buffer.apply(start: 1, end: 2, text: "sir")
        buffer.apply(start: 0, end: 2, text: "Hi Sarah,")
        buffer.apply(start: 2, end: 3, text: "meet at 3:30.")
        #expect(buffer.text == "Hi Sarah, meet at 3:30.")
        buffer.apply(start: .nan, end: 3, text: "invalid")
        buffer.apply(start: 6, end: 2, text: "invalid")
        #expect(buffer.text == "Hi Sarah, meet at 3:30.")
    }
    @Test func longTranscriptRetainsEverySection() {
        var buffer = TranscriptBuffer()
        for i in 0..<600 { buffer.apply(start: Double(i), end: Double(i + 1), text: "word\(i)") }
        #expect(buffer.text.split(separator: " ").count == 600)
        #expect(buffer.text.hasSuffix("word599"))
    }
    @Test func emailFormattingUsesOnlySpokenGreetingAndClosing() {
        let plain = "Please do not cancel. Send 38 invoices, not 37."
        #expect(WritingRules.format(plain, email: true) == plain)
        let email = "Hi Sarah, please send 38 invoices. Thanks, Nathan."
        #expect(WritingRules.format(email, email: true) == "Hi Sarah,\n\nplease send 38 invoices.\n\nThanks,\nNathan")
        #expect(WritingRules.format(email, email: false) == email)
    }
    @Test func unicodeAndLinksStayIntact() {
        let text = "Hi Zoë, send https://example.com/a?q=38 to naïth@example.com."
        #expect(WritingRules.format(text, email: true) == "Hi Zoë,\n\nsend https://example.com/a?q=38 to naïth@example.com.")
    }
    @Test func orbMatchesUpstreamReference() throws {
        struct Fixture: Decodable { let dots: [Dot] }
        struct Dot: Decodable { let x, y, z, r, white, a: Double }
        let url = try #require(Bundle.module.url(forResource: "thinking-orbs-20", withExtension: "json"))
        let expected = try JSONDecoder().decode(Fixture.self, from: Data(contentsOf: url)).dots
        let actual = ThinkingOrb.frame(seconds: 0.6)
        #expect(actual.count == expected.count)
        for (a, e) in zip(actual, expected) {
            #expect(abs(a.x - e.x) < 1e-9); #expect(abs(a.y - e.y) < 1e-9)
            #expect(abs(a.z - e.z) < 1e-9); #expect(abs(a.radius - e.r) < 1e-9)
            #expect(abs(a.white - e.white) < 1e-9); #expect(abs(a.alpha - e.a) < 1e-9)
        }
    }
}
