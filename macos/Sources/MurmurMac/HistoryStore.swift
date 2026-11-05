import Foundation

struct HistoryItem: Codable, Sendable {
    let id: UUID
    let date: Date
    let text: String
    var inserted: Bool
    let audioFile: String?
    var error: String?
}
actor HistoryStore {
    nonisolated let root: URL
    init() {
        root = URL.applicationSupportDirectory.appending(path: "N3moStudio/Murmur")
    }
    func recordingURL() throws -> URL {
        let directory = root.appending(path: "Recordings")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true,
            attributes: [.posixPermissions: 0o700])
        return directory.appending(path: UUID().uuidString + ".caf")
    }
    func save(_ item: HistoryItem) throws {
        let directory = root.appending(path: "History")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true,
            attributes: [.posixPermissions: 0o700])
        try JSONEncoder().encode(item).write(to: directory.appending(path: item.id.uuidString + ".json"), options: .atomic)
    }
    func removeAudio(_ url: URL) throws {
        guard url.deletingLastPathComponent().standardizedFileURL == root.appending(path: "Recordings").standardizedFileURL else { return }
        try FileManager.default.removeItem(at: url)
    }
}
