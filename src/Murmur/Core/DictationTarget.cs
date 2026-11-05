namespace Murmur.Core;

public sealed record Target(nint Window, string Process, string Field, string? Value, string Selection, bool Editable, string Category = "other", int? Caret = null, string StableField = "");

public static class DictationTarget
{
    // Plain dictation chooses its field at stop, unlike selected-text commands.
    // If no field was selected then, an explicitly focused editor can be chosen
    // before insertion. Returning to the same unchanged editor may move the caret.
    public static Target? AtCompletion(Target? stopped, Target current)
    {
        if (!current.Editable || current.Process.Equals(Brand.Name, StringComparison.OrdinalIgnoreCase)) return stopped;
        if (stopped is null || !stopped.Editable || stopped.Process.Equals(Brand.Name, StringComparison.OrdinalIgnoreCase)) return current;
        if (stopped.Window != current.Window || stopped.Value is null || stopped.Value != current.Value) return stopped;
        bool sameField = stopped.Field.Length > 0 && stopped.Field == current.Field;
        bool recreated = stopped.StableField.Length > 0 && stopped.StableField == current.StableField;
        return sameField || recreated ? current : stopped;
    }
}
