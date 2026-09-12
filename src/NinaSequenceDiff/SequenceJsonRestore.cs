using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;

namespace NinaSequenceDiff;

/// <summary>
/// Restores one removed item to an Advanced Sequencer.
///
/// Production recovery works with N.I.N.A.'s live object tree. The JSON helper
/// remains for tests and diagnostics, where it rebuilds reference IDs and
/// parent links before N.I.N.A. deserializes the result.
    /// </summary>
internal static class SequenceJsonRestore {
    /// <summary>
    /// Clones one deleted instruction or block from the baseline tree and
    /// inserts it into the matching location in the current live tree.
///
/// The baseline is first deserialized as a complete N.I.N.A. sequence. This is
/// safer than joining two JSON documents: their $id and $ref values are
/// local to one serialization run and cannot safely be mixed.
    /// </summary>
    public static void RestoreRemovedItem(
        ISequenceRootContainer currentRoot,
        ISequenceRootContainer baselineRoot,
        SequenceDifference difference) {
        ArgumentNullException.ThrowIfNull(currentRoot);
        ArgumentNullException.ThrowIfNull(baselineRoot);
        ArgumentNullException.ThrowIfNull(difference);

        // The visible Restore button should only reach this method for a whole
        // item that the differ identified as removed. Keep the guard here too,
        // because a public helper must protect itself from incorrect callers.
        if (difference.Kind != SequenceDifferenceKind.Removed
            || !difference.IsRemovedSequencerItem) {
            throw new InvalidOperationException(
                "Only a removed Advanced Sequencer instruction can be " +
                "restored.");
        }

        // Each nested Items array contributes one index. For example, a block's
        // third child produces two indices: the block index and child index.
        var itemIndices = GetItemIndices(difference.Path);
        if (itemIndices.Count == 0) {
            throw new InvalidOperationException(
                "The deleted instruction does not have a valid Sequencer " +
                "location.");
        }

        // Read from the old baseline but attach to the current sequence. The
        // two roots intentionally differ because one still contains the item.
        var sourceItem = GetItem(baselineRoot, itemIndices);
        var parentIndices = itemIndices.Take(itemIndices.Count - 1).ToArray();
        var destination = GetContainer(currentRoot, parentIndices);
        var restoredItem = sourceItem.Clone() as ISequenceItem
            ?? throw new InvalidOperationException(
                "N.I.N.A. could not clone the deleted instruction.");

        // Clamp only prevents an invalid collection index. It does not invent a
        // parent: GetContainer above already proved that the original block is
        // still present in the current sequence.
        var insertionIndex = Math.Clamp(
            itemIndices[^1],
            0,
            destination.Items.Count);
        restoredItem.AttachNewParent(destination);

        /*
         * ObservableCollection changes its list before it notifies N.I.N.A.'s
         * visible Sequencer controls. A WPF layout listener can therefore
         * throw an ArgumentException after the item was inserted successfully.
         * Treat that narrow situation as success so the caller removes the
         * recovery row. Otherwise a second click would insert a duplicate.
         * Every other exception still reaches the caller and remains visible.
         */
        var countBeforeInsert = destination.Items.Count;
        try {
            destination.Items.Insert(insertionIndex, restoredItem);
        } catch (ArgumentException) when (
            destination.Items.Count == countBeforeInsert + 1
            && insertionIndex < destination.Items.Count
            && ReferenceEquals(
                destination.Items[insertionIndex],
                restoredItem)) {
            // The collection mutation is complete despite the failed observer.
        }
    }

    /// <summary>
    /// Extracts the index path through N.I.N.A.'s nested Items collections from
    /// a JSON path. Only these indices describe where an instruction belongs.
    /// </summary>
    internal static IReadOnlyList<int> GetItemIndices(string path) {
        var steps = ParsePath(path);
        var indices = new List<int>();

        for (var index = 0; index + 2 < steps.Count; index++) {
            if (steps[index] is PropertyStep { Name: "Items" }
                && steps[index + 1] is PropertyStep { Name: "$values" }
                && steps[index + 2] is ArrayIndexStep itemIndex) {
                indices.Add(itemIndex.Index);
                index += 2;
            }
        }

        return indices;
    }

    /// <summary>
    /// Restores a previous JSON value without a live N.I.N.A. view model.
///
/// This is retained for unit tests and diagnostics. The dockable panel uses
/// RestoreRemovedItem instead, because cloning through N.I.N.A.'s object model
/// is the safer production path.
    /// </summary>
    public static string Restore(
        string currentJson,
        SequenceDifference difference) {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentJson);
        ArgumentNullException.ThrowIfNull(difference);

        if (difference.Kind == SequenceDifferenceKind.Added
            || string.IsNullOrWhiteSpace(difference.BeforeJson)) {
            throw new InvalidOperationException(
                "This change has no previous value to restore.");
        }

        var root = JToken.Parse(currentJson);
        var previousValue = JToken.Parse(difference.BeforeJson);
        var restored = ApplyPreviousValue(root, difference, previousValue);

        // The inserted fragment needs fresh reference IDs and correct Parent
        // references before it can be read as a N.I.N.A. sequence again.
        NormalizeReferences(restored);
        return restored.ToString(Formatting.Indented);
    }

    /// <summary>
    /// Walks the baseline Items hierarchy and returns the item at the recorded
    /// index path. Every intermediate item must be a container or block.
    /// </summary>
    private static ISequenceItem GetItem(
        ISequenceRootContainer root,
        IReadOnlyList<int> itemIndices) {
        ISequenceContainer parent = root;

        for (var index = 0; index < itemIndices.Count; index++) {
            var itemIndex = itemIndices[index];
            if (itemIndex < 0 || itemIndex >= parent.Items.Count) {
                throw new InvalidOperationException(
                    "The last loaded sequence no longer contains the deleted " +
                    "instruction.");
            }

            var item = parent.Items[itemIndex];
            if (index == itemIndices.Count - 1) {
                return item;
            }

            parent = item as ISequenceContainer
                ?? throw new InvalidOperationException(
                    "The deleted instruction no longer has its original " +
                    "parent block.");
        }

        throw new InvalidOperationException(
            "The deleted instruction does not have a valid Sequencer " +
            "location.");
    }

    /// <summary>
    /// Walks the current Items hierarchy to find the parent container into
    /// which the cloned item must be inserted.
    /// </summary>
    private static ISequenceContainer GetContainer(
        ISequenceRootContainer root,
        IReadOnlyList<int> itemIndices) {
        ISequenceContainer parent = root;

        foreach (var itemIndex in itemIndices) {
            if (itemIndex < 0 || itemIndex >= parent.Items.Count) {
                throw new InvalidOperationException(
                    "The original parent block is no longer present in the " +
                    "current sequence.");
            }

            parent = parent.Items[itemIndex] as ISequenceContainer
                ?? throw new InvalidOperationException(
                    "The original parent block is no longer present in the " +
                    "current sequence.");
        }

        return parent;
    }

    /// <summary>
    /// Applies the earlier value to a parsed JSON tree. A removed array entry
    /// is
    /// inserted; a removed property or changed value is replaced in place.
    /// </summary>
    private static JToken ApplyPreviousValue(
        JToken root,
        SequenceDifference difference,
        JToken previousValue) {
        var steps = ParsePath(difference.Path);
        if (steps.Count == 0) {
            // A root-level change has no parent. Its prior value becomes root.
            return previousValue.DeepClone();
        }

        var parent = Resolve(root, steps.Take(steps.Count - 1).ToArray());
        var finalStep = steps[^1];

        if (difference.Kind == SequenceDifferenceKind.Removed
            && finalStep is ArrayIndexStep insertionIndex) {
            if (parent is not JArray array) {
                throw new InvalidOperationException(
                    "The previous sequence item cannot be restored at this " +
                    "location.");
            }

            array.Insert(
                Math.Clamp(insertionIndex.Index, 0, array.Count),
                previousValue.DeepClone());
            return root;
        }

        switch (finalStep) {
            case PropertyStep property when parent is JObject objectParent:
                objectParent[property.Name] = previousValue.DeepClone();
                return root;

            case ArrayIndexStep arrayIndex
                when parent is JArray arrayParent
                && arrayIndex.Index >= 0
                && arrayIndex.Index < arrayParent.Count:
                arrayParent[arrayIndex.Index] = previousValue.DeepClone();
                return root;

            default:
                throw new InvalidOperationException(
                    "The current sequence no longer matches the selected " +
                    "change.");
        }
    }

    /// <summary>
    /// Follows already parsed path steps to the requested JSON value. A missing
    /// property or index means the current tree no longer matches the result.
    /// </summary>
    private static JToken Resolve(
        JToken root,
        IReadOnlyList<JsonPathStep> steps) {
        var current = root;

        foreach (var step in steps) {
            current = step switch {
                PropertyStep property
                    when current is JObject objectCurrent
                    && objectCurrent.TryGetValue(
                        property.Name,
                        out var value) => value,

                ArrayIndexStep index
                    when current is JArray array
                    && index.Index >= 0
                    && index.Index < array.Count => array[index.Index],

                _ => throw new InvalidOperationException(
                    "The current sequence no longer matches the selected " +
                    "change.")
            };
        }

        return current;
    }

    /// <summary>
    /// Parses the small JSON-path language produced by SequenceJsonDiffer.
///
/// Supported steps are ordinary properties (.Name), quoted properties
/// (['Name with spaces']), and array indices ([3]). The parser rejects every
/// other form rather than guessing which part of a sequence to change.
    /// </summary>
    private static IReadOnlyList<JsonPathStep> ParsePath(string path) {
        if (string.IsNullOrWhiteSpace(path) || path[0] != '$') {
            throw new InvalidOperationException(
                "The change does not have a valid sequence location.");
        }

        var steps = new List<JsonPathStep>();

        for (var position = 1; position < path.Length;) {
            if (path[position] == '.') {
                var start = ++position;

                while (position < path.Length
                       && path[position] is not '.' and not '[') {
                    position++;
                }

                if (position == start) {
                    throw new InvalidOperationException(
                        "The change does not have a valid sequence location.");
                }

                steps.Add(new PropertyStep(path[start..position]));
                continue;
            }

            if (path[position] != '[') {
                throw new InvalidOperationException(
                    "The change does not have a valid sequence location.");
            }

            position++;
            if (position < path.Length && path[position] == '\'') {
                position++;
                var property = new System.Text.StringBuilder();

                while (position < path.Length && path[position] != '\'') {
                    if (path[position] == '\\'
                        && position + 1 < path.Length
                        && path[position + 1] == '\'') {
                        property.Append('\'');
                        position += 2;
                    } else {
                        property.Append(path[position++]);
                    }
                }

                if (position >= path.Length
                    || ++position >= path.Length
                    || path[position++] != ']') {
                    throw new InvalidOperationException(
                        "The change does not have a valid sequence location.");
                }

                steps.Add(new PropertyStep(property.ToString()));
                continue;
            }

            var numberStart = position;
            while (position < path.Length && char.IsDigit(path[position])) {
                position++;
            }

            if (numberStart == position
                || position >= path.Length
                || path[position++] != ']'
                || !int.TryParse(
                    path[numberStart..(position - 1)],
                    out var index)) {
                throw new InvalidOperationException(
                    "The change does not have a valid sequence location.");
            }

            steps.Add(new ArrayIndexStep(index));
        }

        return steps;
    }

    /// <summary>
    /// Gives every real object a fresh $id and rebuilds the special Parent
    /// references that N.I.N.A. expects. It also refuses reference shapes that
    /// the plugin cannot prove are safe after an in-place JSON restoration.
    /// </summary>
    private static void NormalizeReferences(JToken root) {
        var state = new ReferenceState();
        NormalizeToken(root, null, null, state);

        if (ContainsUnsupportedReference(root)) {
            throw new InvalidOperationException(
                "The restored value contains an unsupported object reference.");
        }
    }

    // A $ref is safe only when it is the generated Parent link. Other refs
    // could point to an object whose new ID is unknown after restoring JSON.
    private static bool ContainsUnsupportedReference(JToken token) =>
        token switch {
        JObject value
            when value.Property("$ref") is not null
            && value.Parent is not JProperty { Name: "Parent" } => true,

        JObject value => value.Properties()
            .Any(property => ContainsUnsupportedReference(property.Value)),

        JArray value => value.Any(ContainsUnsupportedReference),
        _ => false
    };

    // Dispatch to an object or every array entry. The two parent arguments make
    // it possible to rebuild Parent refs for sequencer items in collections.
    private static void NormalizeToken(
        JToken token,
        JObject? parentForThisObject,
        JObject? parentForCollectionEntries,
        ReferenceState state) {
        switch (token) {
            case JObject objectToken:
                NormalizeObject(
                    objectToken,
                    parentForThisObject,
                    parentForCollectionEntries,
                    state);
                break;

            case JArray array:
                foreach (var child in array) {
                    NormalizeToken(child, null, null, state);
                }
                break;
        }
    }

    /// <summary>
    /// Normalizes one JSON object and then its children. Items, Conditions and
    /// Triggers are special because their entries belong to this sequencer
    /// object and must receive it as their Parent.
    /// </summary>
    private static void NormalizeObject(
        JObject value,
        JObject? parentForThisObject,
        JObject? parentForCollectionEntries,
        ReferenceState state) {
        // A pure reference object is not a real object to renumber.
        if (value.Properties().Count() == 1
            && value["$ref"] is not null) {
            return;
        }

        var id = state.NextId();
        value["$id"] = id;
        state.Add(value, id);

        var isSequencerObject = value["$type"]?.Value<string>()?
            .StartsWith("NINA.Sequencer.", StringComparison.Ordinal) == true;

        if (isSequencerObject && value.Property("Parent") is not null) {
            value["Parent"] = parentForThisObject is null
                ? JValue.CreateNull()
                : new JObject {
                    ["$ref"] = state.IdOf(parentForThisObject)
                };
        }

        foreach (var property in value.Properties().ToArray()) {
            // $id belongs to this object. Parent was already rebuilt above.
            if (property.Name is "$id" or "Parent") {
                continue;
            }

            if (isSequencerObject
                && (property.Name is "Items" or "Conditions" or "Triggers")) {
                NormalizeToken(property.Value, null, value, state);
            } else if (property.Name == "$values"
                       && parentForCollectionEntries is not null
                       && property.Value is JArray entries) {
                foreach (var entry in entries) {
                    NormalizeToken(
                        entry,
                        parentForCollectionEntries,
                        null,
                        state);
                }
            } else {
                NormalizeToken(property.Value, null, null, state);
            }
        }
    }

    // These two small records make parsed paths type-safe. A caller cannot
    // confuse a property name such as "3" with the third array entry.
    private abstract record JsonPathStep;

    private sealed record PropertyStep(string Name) : JsonPathStep;

    private sealed record ArrayIndexStep(int Index) : JsonPathStep;

    /// <summary>
    /// Keeps object identity separate from JSON text while NormalizeObject
    /// walks the tree. Reference equality is essential: two objects may have
    /// equal fields but still need different $id values.
    /// </summary>
    private sealed class ReferenceState {
        private readonly Dictionary<JObject, string> ids =
            new(ReferenceEqualityComparer.Instance);
        private int nextId = 1;

        // N.I.N.A.'s serializer uses simple increasing string IDs.
        public string NextId() => nextId++.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

        public string IdOf(JObject value) => ids.TryGetValue(
            value,
            out var id)
            ? id
            : throw new InvalidOperationException(
                "The restored sequence has an invalid parent relation.");

        public void Add(JObject value, string id) => ids.Add(value, id);
    }
}
