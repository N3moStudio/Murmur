using System.Text.Json;

namespace Murmur.Core;

public static class Brand
{
    public const string Name = "Murmur", Publisher = "N3mo Studio", AppId = "N3moStudio.Murmur", Version = "0.1.3";
}
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new InvalidDataException("Empty data.");
}
public sealed record LibraryEntry(string Id, string Kind, string Trigger, string Value, bool Shared = false);
public sealed record Segment(double Start, double End, string Text, string Speaker = "You");
public sealed record HistoryItem(string Id, DateTimeOffset Created, string Text, string Raw, string? Audio, string Target, string Status, double Duration, double TranscriptionMs, double CleanupMs, string? Error = null);
public sealed record Meeting(string Id, string Title, DateTimeOffset Created, string Notes = "", string Summary = "", bool SummaryEdited = false, string Transcript = "", List<Segment>? Segments = null, List<string>? Audio = null, string? FolderId = null, string Status = "draft", double Duration = 0);
public sealed record Folder(string Id, string Name, string Description = "", bool Shared = false);
public sealed record StoredDocument(string Kind, string Id, string Json, DateTimeOffset Updated, bool Deleted = false);
public sealed record Transform(string Id, string Name, string Prompt, string Shortcut = "", List<string>? Samples = null);
public sealed record AppStyle(string Process, string Category, string Style);
public sealed record ModelSpec(string File, long Bytes, string Sha256, string Url, string License, string Repo = "", string Revision = "");
public sealed record Settings
{
    public bool Onboarded { get; init; }
    public string Theme { get; init; } = "system";
    public string Microphone { get; init; } = "default";
    public string Language { get; init; } = "en";
    public string SpeechModel { get; init; } = "moonshine-base-en";
    public string TextModel { get; init; } = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
    public string Cleanup { get; init; } = "light";
    public string Style { get; init; } = "formal";
    public string HoldShortcut { get; init; } = "Ctrl+Alt";
    public string ToggleShortcut { get; init; } = "Double Caps Lock";
    public int ShortcutVersion { get; init; }
    public string CommandShortcut { get; init; } = "Ctrl+Alt+Shift";
    public bool StartAtLogin { get; init; }
    public bool ShowOverlay { get; init; }
    public string OverlayEdge { get; init; } = "bottom";
    public bool RetainHistory { get; init; } = true;
    public bool RetainAudio { get; init; }
    public int RetentionDays { get; init; } = 30;
    public bool SoundFeedback { get; init; }
    public bool SubmitAfterInsert { get; init; }
    public bool ContextAccess { get; init; }
    public bool DetectWritingContext { get; init; } = true;
    public string EmailStyle { get; init; } = "formal";
    public string PersonalStyle { get; init; } = "formal";
    public string WorkStyle { get; init; } = "formal";
    public bool McpEnabled { get; init; }
    public bool CloudEnabled { get; init; }
    public string CloudEndpoint { get; init; } = "";
    public string CloudModel { get; init; } = "";
    public string SharedFolder { get; init; } = "";
    public string UpdateFeed { get; init; } = "";
    public string UpdatePublicKey { get; init; } = "";
    public bool MeetingSystemAudio { get; init; } = true;
    public bool IdentifySpeakers { get; init; }
    public string OrbAnimation { get; init; } = "on";
    public bool CalendarReminders { get; init; } = true;
    public int MeetingLimitMinutes { get; init; } = 120;
    public List<AppStyle> AppStyles { get; init; } = [];
}
public enum RecordingState { Idle, Recording, Processing, Completed, Failed }
public sealed class RecordingMachine
{
    public RecordingState State { get; private set; }
    public bool Start() { if (State is RecordingState.Recording or RecordingState.Processing) return false; State = RecordingState.Recording; return true; }
    public bool Stop() { if (State != RecordingState.Recording) return false; State = RecordingState.Processing; return true; }
    public void Finish(bool success) { if (State == RecordingState.Processing) State = success ? RecordingState.Completed : RecordingState.Failed; }
    public void Cancel() => State = RecordingState.Idle;
}
