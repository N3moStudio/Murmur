import AVFoundation
import Speech
import CoreMedia
import MurmurCore

actor SpeechSession {
    private let engine = AVAudioEngine()
    private var continuation: AsyncThrowingStream<AnalyzerInput, any Error>.Continuation?
    private var analyzer: SpeechAnalyzer?
    private var result: Task<String, Error>?
    private var hasTap = false
    private var runID: UUID?

    func prepareEnglish() async throws {
        let transcriber = try await englishTranscriber()
        if let request = try await AssetInventory.assetInstallationRequest(supporting: [transcriber]) {
            try await request.downloadAndInstall()
        }
        guard await SpeechAnalyzer.bestAvailableAudioFormat(compatibleWith: [transcriber]) != nil else {
            throw MurmurError.message("The English speech download is not ready. Retry setup when the download has finished.")
        }
    }
    private func englishTranscriber() async throws -> SpeechTranscriber {
        guard SpeechTranscriber.isAvailable,
              let locale = await SpeechTranscriber.supportedLocale(equivalentTo: Locale(identifier: "en-US")) else {
            throw MurmurError.message("This preview requires a Mac that supports on-device SpeechTranscriber and macOS 26 or later.")
        }
        return SpeechTranscriber(locale: locale, preset: .timeIndexedProgressiveTranscription)
    }
    func start(recoveryURL: URL) async throws {
        guard result == nil, runID == nil else { throw MurmurError.message("Finish the current dictation first.") }
        let token = UUID(); runID = token
        let transcriber = try await englishTranscriber()
        guard runID == token else { throw CancellationError() }
        guard let format = await SpeechAnalyzer.bestAvailableAudioFormat(compatibleWith: [transcriber]) else {
            throw MurmurError.message("Download English speech in Murmur setup first.")
        }
        guard runID == token else { throw CancellationError() }
        let analyzer = SpeechAnalyzer(modules: [transcriber]); self.analyzer = analyzer
        let pair = AsyncThrowingStream<AnalyzerInput, any Error>.makeStream(bufferingPolicy: .bufferingOldest(128))
        continuation = pair.continuation
        let file = try AVAudioFile(forWriting: recoveryURL, settings: format.settings)
        try installCapture(engine, format: format, file: file, continuation: pair.continuation)
        hasTap = true
        do { engine.prepare(); try engine.start() }
        catch { engine.inputNode.removeTap(onBus: 0); hasTap = false; continuation?.finish(throwing: error); throw error }
        let reader = Task {
            var buffer = TranscriptBuffer()
            for try await update in transcriber.results {
                buffer.apply(start: update.range.start.seconds, end: CMTimeRangeGetEnd(update.range).seconds,
                    text: String(update.text.characters))
            }
            return buffer.text
        }
        result = Task {
            do {
                try await analyzer.prepareToAnalyze(in: format)
                if let last = try await analyzer.analyzeSequence(pair.stream) { try await analyzer.finalizeAndFinish(through: last) }
                else { await analyzer.cancelAndFinishNow() }
                return try await reader.value
            } catch {
                await analyzer.cancelAndFinishNow(); reader.cancel(); throw error
            }
        }
    }
    func waitForResult() async throws -> String {
        guard let result else { throw MurmurError.message("Recording did not start.") }
        defer { self.result = nil; analyzer = nil; continuation = nil; runID = nil }
        return try await result.value
    }
    func finishInput() {
        engine.stop()
        if hasTap { engine.inputNode.removeTap(onBus: 0); hasTap = false }
        continuation?.finish()
    }
    func cancel() async {
        runID = nil
        finishInput(); result?.cancel()
        if let analyzer { await analyzer.cancelAndFinishNow() }
        if let result { _ = try? await result.value }
        result = nil; analyzer = nil; continuation = nil
    }
}

// Form the SDK callback outside actor isolation; AVAudioEngine invokes it on
// its audio thread. Only this serial tap touches its converter and file.
nonisolated private func installCapture(_ engine: AVAudioEngine, format: AVAudioFormat, file: AVAudioFile,
    continuation: AsyncThrowingStream<AnalyzerInput, any Error>.Continuation) throws {
    let input = engine.inputNode, source = input.outputFormat(forBus: 0)
    guard source.channelCount > 0, source.sampleRate > 0,
          let converter = AVAudioConverter(from: source, to: format) else { throw MurmurError.message("No compatible microphone is available.") }
    converter.primeMethod = .none
    let ratio = format.sampleRate / source.sampleRate
    input.installTap(onBus: 0, bufferSize: AVAudioFrameCount(source.sampleRate / 10), format: source) { buffer, _ in
        let capacity = AVAudioFrameCount(ceil(Double(buffer.frameLength) * ratio))
        guard let output = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: capacity) else {
            continuation.finish(throwing: MurmurError.message("Could not allocate microphone audio.")); return
        }
        var supplied = false; var failure: NSError?
        let status = converter.convert(to: output, error: &failure) { _, inputStatus in
            if supplied { inputStatus.pointee = .noDataNow; return nil }
            supplied = true; inputStatus.pointee = .haveData; return buffer
        }
        guard status != .error else {
            if let failure { continuation.finish(throwing: failure) }
            else { continuation.finish(throwing: MurmurError.message("Audio conversion failed.")) }
            return
        }
        guard output.frameLength > 0 else { return }
        do { try file.write(from: output) } catch { continuation.finish(throwing: error); return }
        switch continuation.yield(AnalyzerInput(buffer: output)) {
        case .dropped(_): continuation.finish(throwing: MurmurError.message("Speech processing fell behind. The recording has been kept for recovery."))
        default: break
        }
    }
}
