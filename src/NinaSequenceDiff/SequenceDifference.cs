using System.Globalization;
using System.Windows.Input;

namespace NinaSequenceDiff;

// The comparison can see three kinds of change. The user interface currently
// shows only Removed entries, but keeping the complete vocabulary here makes
// the comparison engine useful and testable on its own.
public enum SequenceDifferenceKind {
    Added,
    Removed,
    Changed
}

/// <summary>
/// One result row from comparing the sequence that was loaded with the
/// sequence that is currently on screen.
///
/// The readable properties are for the table. The raw JSON properties are
/// deliberately internal: they are needed to restore the exact original item,
/// but exposing them would make the table confusing and invite unsafe edits.
/// </summary>
public sealed class SequenceDifference {
    internal SequenceDifference(
        SequenceDifferenceKind kind,
        string path,
        string location,
        string before,
        string after,
        string beforeToolTip,
        string afterToolTip,
        string? beforeJson,
        string? afterJson,
        bool isRemovedSequencerItem,
        string instructionText,
        string instructionDetails) {
        Kind = kind;
        Path = path;
        Location = location;
        Before = before;
        After = after;
        BeforeToolTip = beforeToolTip;
        AfterToolTip = afterToolTip;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        IsRemovedSequencerItem = isRemovedSequencerItem;
        InstructionText = instructionText;
        InstructionDetails = instructionDetails;
    }

    // The differ does not know when N.I.N.A. changed the tree. The view model
    // assigns this timestamp the first time it notices this particular change.
    public DateTime ChangedAt { get; internal set; }

    // A fixed, sortable-looking format prevents regional date settings from
    // turning an unambiguous time into, for example, 03/04 versus 04/03.
    public string ChangedAtText => ChangedAt.ToString(
        "yyyy-MM-dd HH:mm:ss",
        CultureInfo.InvariantCulture);

    public SequenceDifferenceKind Kind { get; }

    public string KindText => Kind.ToString();

    /// <summary>
    /// Internal JSON address of the original value. It is a map for code, not
    /// a label for users; restore parses it to locate the old item and parent.
    /// </summary>
    public string Path { get; }

    // Human-readable descriptions retained for diagnostics and possible future
    // user interfaces. The focused recovery table uses Instruction instead.
    public string Location { get; }

    // Short one-line representations. These are intentionally separate from
    // BeforeJson/AfterJson, which may contain a large object graph.
    public string Before { get; }

    public string After { get; }

    // Tool-tip-ready text. N.I.N.A.'s current DataGrid does not expose these
    // in every theme, but keeping them here avoids losing useful context.
    public string BeforeToolTip { get; }

    public string AfterToolTip { get; }

    /// <summary>
    /// True only for a complete Advanced Sequencer instruction or block
    /// removed from an Items collection. Property-level edits
    /// deliberately remain false so Sequencer Item Recovery can focus on
    /// recoverable deletions.
    /// </summary>
    public bool IsRemovedSequencerItem { get; }

    /// <summary>
    /// The same visible item name used by the Advanced Sequencer. This is what
    /// lets a person recognise an exposure, block or wait without reading JSON.
    /// </summary>
    public string InstructionText { get; }

    /// <summary>
    /// Short, human-readable settings that identify the removed item.
    /// </summary>
    public string InstructionDetails { get; }

    public string LocationToolTip => Location;

    public string KindToolTip => KindText;

    public string ChangedAtToolTip => ChangedAtText;

    // Compact JSON is retained only while this result exists. It is the source
    // material for an exact restore and is never written to a separate history.
    internal string? BeforeJson { get; }

    internal string? AfterJson { get; }

    // A deleted item can originate from the loaded JSON file or from the
    // short-lived in-memory snapshot that noticed a newly added item. Restore
    // needs to deserialize the matching source, not merely the main baseline.
    internal string? RestoreBaselineJson { get; set; }

    // This private, unambiguous key lets the view model recognise the same
    // deletion on the next one-second refresh and keep its original timestamp.
    internal string ChangeKey =>
        $"{Kind}\u001f{Path}\u001f{BeforeJson}\u001f{AfterJson}";

    // The view model creates a command for recoverable removals only. A normal
    // property edit therefore can never accidentally acquire a Restore button.
    public ICommand? RestoreCommand { get; internal set; }
}
