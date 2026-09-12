namespace NinaSequenceDiff;

/// <summary>
/// Keeps the working recovery snapshot for a sequence that has not been
/// loaded from a JSON file. While no deletion is visible, the snapshot follows
/// the user's normal construction work. The first missing item keeps the last
/// complete snapshot intact, so it can be restored.
/// </summary>
internal sealed class InMemorySequenceBaseline {
    /// <summary>
    /// Starts with the first complete Advanced Sequencer tree observed by the
    /// panel. This can be an empty new sequence or one already being composed.
    /// </summary>
    public InMemorySequenceBaseline(string initialJson) {
        Json = initialJson;
    }

    /// <summary>
    /// Gets the JSON snapshot that will be compared with the live sequence.
    /// </summary>
    public string Json { get; private set; }

    /// <summary>
    /// Follows intentional additions and edits only while every item from the
    /// previous snapshot still exists. A detected deletion returns false and
    /// deliberately leaves the last complete snapshot unchanged for recovery.
    /// </summary>
    public bool AdvanceWhenNothingWasRemoved(
        string currentJson,
        bool hasRemovedItems) {
        if (hasRemovedItems) {
            return false;
        }

        Json = currentJson;
        return true;
    }
}
