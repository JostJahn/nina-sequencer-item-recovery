using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace NinaSequenceDiff;

/// <summary>
/// Compares two N.I.N.A. sequence JSON documents as a tree, rather than as
/// lines of text.
///
/// N.I.N.A. serialises both the useful sequence definition and temporary
/// runtime/reference information into JSON. This class ignores the temporary
/// part and identifies complete removed instructions or blocks that can be
/// safely restored by the rest of the plugin.
/// </summary>
public static class SequenceJsonDiffer {
    // These fields may change while a sequence runs or may only describe how
    // Newtonsoft JSON connected objects during serialisation. Treating them as
    // user edits would create false alarms and, more importantly, unsafe
    // restore candidates.
    private static readonly HashSet<string> IgnoredPropertyNames = new(StringComparer.Ordinal) {
        "$id",
        "$ref",
        "Parent",
        "Status",
        "Progress",
        "HasChanges",
        "IsExpanded",
        "IsInitialized",
        "IsRunning"
    };

    public static IReadOnlyList<SequenceDifference> Compare(string beforeJson, string afterJson, int maximumDifferences = 1_000) {
        // General-purpose entry point retained for unit tests and diagnostics.
        // The current panel uses the more focused method below.
        return CompareInternal(beforeJson, afterJson, maximumDifferences, static _ => true);
    }

    /// <summary>
    /// Finds only complete Advanced Sequencer items that have disappeared from
    /// the current tree. Changed properties and additions do not consume the
    /// display limit, so a busy sequence still exposes its deleted items.
    /// </summary>
    public static IReadOnlyList<SequenceDifference> CompareRemovedSequencerItems(string beforeJson, string afterJson, int maximumItems = 1_000) {
        // This filter is the safety boundary of the visible feature: additions
        // and changed settings are deliberately not offered a Restore action.
        return CompareInternal(beforeJson, afterJson, maximumItems, static difference => difference.IsRemovedSequencerItem);
    }

    /// <summary>
    /// Builds one recovery row from an exact item path captured by N.I.N.A.'s
    /// collection event. This is the fallback for a removal that cannot be
    /// aligned reliably by comparing the surrounding JSON arrays.
    /// </summary>
    internal static SequenceDifference? CreateRemovedSequencerItemAtPath(
        string baselineJson,
        string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var token = JToken.Parse(baselineJson).SelectToken(path);
        if (token is not JObject item || !IsSequencerItem(item)) {
            return null;
        }

        var differences = new List<SequenceDifference>();
        Add(
            differences,
            1,
            SequenceDifferenceKind.Removed,
            path,
            item,
            null,
            GetSequenceInstruction(item),
            static _ => true);
        return differences.SingleOrDefault();
    }

    private static IReadOnlyList<SequenceDifference> CompareInternal(
        string beforeJson,
        string afterJson,
        int maximumDifferences,
        Func<SequenceDifference, bool> includeDifference) {
        // Failing early produces a clear error in the panel instead of parsing
        // an empty string and reporting a misleading "no changes" result.
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterJson);

        if (maximumDifferences < 1) {
            throw new ArgumentOutOfRangeException(nameof(maximumDifferences));
        }

        // The limit prevents a damaged or radically replaced sequence from
        // allocating an unlimited result list every second.
        var differences = new List<SequenceDifference>();
        CompareToken(JToken.Parse(beforeJson), JToken.Parse(afterJson), "$", null, differences, maximumDifferences, includeDifference);
        return differences;
    }

    private static void CompareToken(
        JToken before,
        JToken after,
        string path,
        string? instruction,
        ICollection<SequenceDifference> differences,
        int limit,
        Func<SequenceDifference, bool> includeDifference) {
        // Every recursive branch observes the same limit, so it can stop work
        // promptly once the panel has enough rows to show.
        if (differences.Count >= limit) {
            return;
        }

        // Carry the nearest readable sequence item down the JSON tree. A child
        // value such as ExposureTime otherwise has no useful name of its own.
        var effectiveInstruction = GetSequenceInstruction(after as JObject)
            ?? GetSequenceInstruction(before as JObject)
            ?? instruction;

        // A string becoming an object, for example, cannot be compared field by
        // field. It is one changed value at this path.
        if (before.Type != after.Type) {
            Add(differences, limit, SequenceDifferenceKind.Changed, path, before, after, effectiveInstruction, includeDifference);
            return;
        }

        switch (before) {
            case JObject beforeObject when after is JObject afterObject:
                CompareObject(beforeObject, afterObject, path, effectiveInstruction, differences, limit, includeDifference);
                break;
            case JArray beforeArray when after is JArray afterArray:
                CompareArray(beforeArray, afterArray, path, effectiveInstruction, differences, limit, includeDifference);
                break;
            default:
                // Scalars (numbers, strings, booleans and null) have no child
                // nodes, so a deep equality check is sufficient.
                if (!JToken.DeepEquals(before, after)) {
                    Add(differences, limit, SequenceDifferenceKind.Changed, path, before, after, effectiveInstruction, includeDifference);
                }
                break;
        }
    }

    private static void CompareObject(
        JObject before,
        JObject after,
        string path,
        string? instruction,
        ICollection<SequenceDifference> differences,
        int limit,
        Func<SequenceDifference, bool> includeDifference) {
        // Union the field names so a property that exists on only one side
        // becomes an Added or Removed result. Sorting makes test and UI output
        // reproducible regardless of the original JSON property order.
        var properties = before.Properties()
            .Concat(after.Properties())
            .Select(property => property.Name)
            .Where(name => !IgnoredPropertyNames.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var propertyName in properties) {
            if (differences.Count >= limit) {
                return;
            }

            var propertyPath = AppendProperty(path, propertyName);
            var hasBefore = before.TryGetValue(propertyName, out var beforeValue);
            var hasAfter = after.TryGetValue(propertyName, out var afterValue);

            if (!hasBefore) {
                // Present only after the edit.
                Add(differences, limit, SequenceDifferenceKind.Added, propertyPath, null, afterValue!, instruction, includeDifference);
            } else if (!hasAfter) {
                // Present only in the loaded baseline.
                Add(differences, limit, SequenceDifferenceKind.Removed, propertyPath, beforeValue!, null, instruction, includeDifference);
            } else {
                CompareToken(beforeValue!, afterValue!, propertyPath, instruction, differences, limit, includeDifference);
            }
        }
    }

    private static void CompareArray(
        JArray before,
        JArray after,
        string path,
        string? instruction,
        ICollection<SequenceDifference> differences,
        int limit,
        Func<SequenceDifference, bool> includeDifference) {
        // Sequencer arrays contain instructions, blocks, conditions, or
        // triggers.
        // Matching them by identity is much better than matching by position:
        // deleting row 2 must not make every later row look changed. The size
        // guard keeps the O(n*m) matching algorithm inexpensive.
        if (IsSequenceEntityArray(before) && IsSequenceEntityArray(after) && before.Count * after.Count <= 2_500) {
            CompareEntityArray(before, after, path, differences, limit, includeDifference);
            return;
        }

        // Ordinary arrays have no reliable item identity. Compare their shared
        // positions, then report the extra tail entries on either side.
        var commonLength = Math.Min(before.Count, after.Count);
        for (var index = 0; index < commonLength && differences.Count < limit; index++) {
            CompareToken(before[index], after[index], AppendIndex(path, index), instruction, differences, limit, includeDifference);
        }

        for (var index = commonLength; index < before.Count && differences.Count < limit; index++) {
            Add(differences, limit, SequenceDifferenceKind.Removed, AppendIndex(path, index), before[index], null, instruction, includeDifference);
        }

        for (var index = commonLength; index < after.Count && differences.Count < limit; index++) {
            Add(differences, limit, SequenceDifferenceKind.Added, AppendIndex(path, index), null, after[index], instruction, includeDifference);
        }
    }

    /// <summary>
    /// Aligns instruction, condition, and trigger lists by a stable key. A
    /// deleted instruction therefore remains one removal rather than changing
    /// every item after its original position.
    ///
    /// The key is intentionally modest: type plus visible name. It is not a
    /// permanent N.I.N.A. identifier; it merely helps recognize the same item
    /// in the loaded and current snapshots.
    /// </summary>
    private static void CompareEntityArray(
        JArray before,
        JArray after,
        string path,
        ICollection<SequenceDifference> differences,
        int limit,
        Func<SequenceDifference, bool> includeDifference) {
        // First find the sequence of entries that stayed in the same relative
        // order. Everything between two matches is an insertion or removal.
        var keysBefore = before.Select(EntityKey).ToArray();
        var keysAfter = after.Select(EntityKey).ToArray();
        var matches = LongestCommonSubsequence(keysBefore, keysAfter);
        var beforeIndex = 0;
        var afterIndex = 0;

        foreach (var match in matches) {
            // Baseline entries before the next match disappeared.
            while (beforeIndex < match.BeforeIndex && differences.Count < limit) {
                Add(differences, limit, SequenceDifferenceKind.Removed, AppendIndex(path, beforeIndex), before[beforeIndex], null, GetSequenceInstruction(before[beforeIndex] as JObject), includeDifference);
                beforeIndex++;
            }

            // Current entries before the next match were newly added.
            while (afterIndex < match.AfterIndex && differences.Count < limit) {
                Add(differences, limit, SequenceDifferenceKind.Added, AppendIndex(path, afterIndex), null, after[afterIndex], GetSequenceInstruction(after[afterIndex] as JObject), includeDifference);
                afterIndex++;
            }

            if (differences.Count >= limit) {
                return;
            }

            // The matched item can still have changed settings, so compare its
            // contents as well. The visible recovery filter drops those edits.
            CompareToken(before[beforeIndex], after[afterIndex], AppendIndex(path, afterIndex), GetSequenceInstruction(after[afterIndex] as JObject), differences, limit, includeDifference);
            beforeIndex++;
            afterIndex++;
        }

        while (beforeIndex < before.Count && differences.Count < limit) {
            Add(differences, limit, SequenceDifferenceKind.Removed, AppendIndex(path, beforeIndex), before[beforeIndex], null, GetSequenceInstruction(before[beforeIndex] as JObject), includeDifference);
            beforeIndex++;
        }

        while (afterIndex < after.Count && differences.Count < limit) {
            Add(differences, limit, SequenceDifferenceKind.Added, AppendIndex(path, afterIndex), null, after[afterIndex], GetSequenceInstruction(after[afterIndex] as JObject), includeDifference);
            afterIndex++;
        }
    }

    private static IReadOnlyList<(int BeforeIndex, int AfterIndex)> LongestCommonSubsequence(IReadOnlyList<string> before, IReadOnlyList<string> after) {
        // Dynamic-programming table: each cell says how many matching entries
        // remain if we start at one pair of positions. It makes row deletions
        // stable without relying on N.I.N.A.'s transient JSON reference IDs.
        var lengths = new int[before.Count + 1, after.Count + 1];
        for (var beforeIndex = before.Count - 1; beforeIndex >= 0; beforeIndex--) {
            for (var afterIndex = after.Count - 1; afterIndex >= 0; afterIndex--) {
                lengths[beforeIndex, afterIndex] = before[beforeIndex] == after[afterIndex]
                    ? lengths[beforeIndex + 1, afterIndex + 1] + 1
                    : Math.Max(lengths[beforeIndex + 1, afterIndex], lengths[beforeIndex, afterIndex + 1]);
            }
        }

        // Walk the finished table from the beginning to recover the actual
        // matching index pairs, not just their number.
        var matches = new List<(int BeforeIndex, int AfterIndex)>();
        var left = 0;
        var right = 0;
        while (left < before.Count && right < after.Count) {
            if (before[left] == after[right]) {
                matches.Add((left++, right++));
            } else if (lengths[left + 1, right] >= lengths[left, right + 1]) {
                left++;
            } else {
                right++;
            }
        }

        return matches;
    }

    // A typed object is N.I.N.A.'s normal representation for sequence entities.
    // Untyped arrays instead use the simpler positional comparison above.
    private static bool IsSequenceEntityArray(JArray array) => array.All(token => token is JObject entity && entity["$type"] is not null);

    private static string EntityKey(JToken token) {
        // Prefer names users recognise. If an item has no name, its type still
        // separates it from a different kind of sequencer object.
        var entity = (JObject)token;
        var type = entity["$type"]?.Value<string>() ?? string.Empty;
        var name = entity["Name"]?.Value<string>()
            ?? entity["TargetName"]?.Value<string>()
            ?? entity["Category"]?.Value<string>()
            ?? string.Empty;
        return $"{type}\u001f{name}";
    }

    private static void Add(
        ICollection<SequenceDifference> differences,
        int limit,
        SequenceDifferenceKind kind,
        string path,
        JToken? before,
        JToken? after,
        string? instruction,
        Func<SequenceDifference, bool> includeDifference) {
        if (differences.Count >= limit) {
            return;
        }

        // Build both machine-useful and person-friendly forms once. The raw
        // JSON remains internal; DescribeValue removes $id and Parent noise.
        var beforeValue = before is null ? string.Empty : DescribeValue(before);
        var afterValue = after is null ? string.Empty : DescribeValue(after);
        var beforeInstruction = instruction ?? GetSequenceInstruction(before as JObject);
        var afterInstruction = instruction ?? GetSequenceInstruction(after as JObject);
        // Only a complete typed Sequencer item may be cloned back into the live
        // model. A removed property, condition setting or JSON helper object is
        // never marked as recoverable.
        var removedSequencerItem = kind == SequenceDifferenceKind.Removed && IsSequencerItem(before as JObject);
        var instructionText = removedSequencerItem ? beforeInstruction ?? string.Empty : string.Empty;
        var instructionDetails = removedSequencerItem ? DescribeInstructionDetails((JObject)before!) : string.Empty;
        var difference = new SequenceDifference(
            kind,
            path,
            DescribeLocation(path),
            ToSingleLine(beforeValue),
            ToSingleLine(afterValue),
            CreateValueToolTip(beforeInstruction, beforeValue),
            CreateValueToolTip(afterInstruction, afterValue),
            before?.ToString(Formatting.None),
            after?.ToString(Formatting.None),
            removedSequencerItem,
            instructionText,
            instructionDetails);

        if (includeDifference(difference)) {
            differences.Add(difference);
        }
    }

    // Extends the internal path to one object property. JSONPath allows simple
    // property names after a dot; punctuation needs the quoted bracket form.
    private static string AppendProperty(string path, string propertyName) => propertyName.All(character => char.IsLetterOrDigit(character) || character == '_')
        ? $"{path}.{propertyName}"
        : path + "['" + propertyName.Replace("'", "\\'") + "']";

    // Array positions are always written as numeric bracket steps, for example
    // "$[2]". SequenceJsonRestore parses the same notation later.
    private static string AppendIndex(string path, int index) => $"{path}[{index}]";

    // Turns a machine JSON path into a brief label for diagnostics. The normal
    // recovery table uses the more useful InstructionText instead.
    private static string DescribeLocation(string path) {
        var readable = path
            .Replace("['$values']", string.Empty, StringComparison.Ordinal)
            .Replace("$", "Advanced sequence", StringComparison.Ordinal);
        readable = readable.Replace('.', ' ').Replace("[", " / ").Replace("]", string.Empty, StringComparison.Ordinal);
        return Humanize(readable);
    }

    // Retains a fuller text form for themes or future views that can show a
    // tooltip, even though the current N.I.N.A. grid uses visible text only.
    private static string CreateValueToolTip(string? instruction, string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return instruction is null ? "No previous value" : $"Sequence item: {instruction}\nNo previous value";
        }

        return instruction is null ? $"Value: {value}" : $"Sequence item: {instruction}\nValue: {value}";
    }

    // Keeps diagnostic cells compact by replacing embedded newlines with a
    // visible separator instead of allowing a JSON value to create extra rows.
    private static string ToSingleLine(string value) => value
        .Replace("\r\n", " · ", StringComparison.Ordinal)
        .Replace("\n", " · ", StringComparison.Ordinal)
        .Replace("\r", " · ", StringComparison.Ordinal);

    // Selects the simplest readable representation for every JSON token type.
    private static string DescribeValue(JToken token) => token switch {
        JValue value => DescribeScalar(value),
        JObject value => DescribeObject(value),
        JArray value => string.Join(Environment.NewLine, value.Select(DescribeValue)),
        _ => token.ToString(Formatting.None)
    };

    // Scalars already contain a user value. Invariant formatting keeps numbers
    // recognizable even when a sequence is shared between regional settings.
    private static string DescribeScalar(JValue value) {
        if (value.Type == JTokenType.Null || value.Value is null) {
            return "(none)";
        }

        return Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    // Objects may be an instruction, a collection wrapper, or ordinary named
    // settings. Prefer the form that is useful to a Sequencer user.
    private static string DescribeObject(JObject value) {
        var instruction = GetSequenceInstruction(value);
        if (instruction is not null) {
            return instruction;
        }

        if (value.TryGetValue("$values", out var values) && values is JArray valueArray) {
            return valueArray.Count == 0
                ? "(empty)"
                : string.Join(Environment.NewLine, valueArray.Select(DescribeValue));
        }

        var fields = value.Properties()
            .Where(property => !IgnoredPropertyNames.Contains(property.Name))
            .Select(property => $"{Humanize(property.Name)}: {DescribeValue(property.Value)}");
        return string.Join(Environment.NewLine, fields);
    }

    // Adds a few useful settings beside the main instruction name. Four values
    // keep a recovery row identifiable without making it too tall.
    private static string DescribeInstructionDetails(JObject instruction) {
        var details = new List<string>();
        foreach (var property in instruction.Properties()) {
            if (property.Name is "$id" or "$ref" or "$type" or "Parent" or "Name" || IgnoredPropertyNames.Contains(property.Name)) {
                continue;
            }

            if (property.Name is "Items" or "Conditions" or "Triggers") {
                var count = CountCollectionEntries(property.Value);
                if (count > 0) {
                    details.Add($"{Humanize(property.Name)}: {count}");
                }

                continue;
            }

            var value = DescribeInstructionSetting(property.Value);
            if (!string.IsNullOrWhiteSpace(value)) {
                details.Add($"{Humanize(property.Name)}: {value}");
            }

            if (details.Count == 4) {
                break;
            }
        }

        return string.Join(" · ", details);
    }

    // N.I.N.A. may serialize a collection directly or wrap it in $values.
    // Support both forms so block, condition, and trigger counts stay useful.
    private static int CountCollectionEntries(JToken value) {
        if (value is JArray array) {
            return array.Count;
        }

        return value is JObject collection && collection["$values"] is JArray values
            ? values.Count
            : 0;
    }

    // Makes a nested setting compact. Arrays contribute only their first three
    // non-empty entries, which avoids a huge table cell for a long list.
    private static string DescribeInstructionSetting(JToken value) => value switch {
        JValue scalar => DescribeScalar(scalar),
        JObject item => FindVisibleName(item),
        JArray array => string.Join(", ", array.Select(DescribeInstructionSetting).Where(text => !string.IsNullOrWhiteSpace(text)).Take(3)),
        _ => string.Empty
    };

    // This is deliberately a small list of familiar display properties. It
    // avoids falling back to a cryptic JSON type name for nested settings.
    private static string FindVisibleName(JObject value) => value["Name"]?.Value<string>()
        ?? value["TargetName"]?.Value<string>()
        ?? value["FilterName"]?.Value<string>()
        ?? string.Empty;

    // Only these N.I.N.A. object families are whole items that can be cloned
    // back into the live Advanced Sequencer.
    private static bool IsSequencerItem(JObject? value) {
        var typeName = value?["$type"]?.Value<string>();
        return typeName?.Contains(".SequenceItem.", StringComparison.Ordinal) == true
            || typeName?.Contains(".Container.", StringComparison.Ordinal) == true;
    }

    // Returns exactly the kind of name a person expects in the Sequencer.
    // An unnamed item falls back to its last type-name segment.
    private static string? GetSequenceInstruction(JObject? value) {
        if (value?["$type"]?.Value<string>() is not { } type || !IsSequencerItem(value)) {
            return null;
        }

        var name = value["Name"]?.Value<string>();
        return !string.IsNullOrWhiteSpace(name) ? name : Humanize(TypeName(type));
    }

    // A serialized type includes namespace and assembly information. The final
    // segment is enough for a readable fallback such as "Exposure".
    private static string TypeName(string fullyQualifiedType) {
        var typeWithoutAssembly = fullyQualifiedType.Split(',', 2)[0];
        return typeWithoutAssembly[(typeWithoutAssembly.LastIndexOf('.') + 1)..];
    }

    // Splits PascalCase words without changing the original JSON or type name.
    // For example, "ExposureTime" becomes "Exposure Time".
    private static string Humanize(string text) {
        if (string.IsNullOrEmpty(text)) {
            return text;
        }

        var builder = new StringBuilder(text.Length + 8);
        for (var index = 0; index < text.Length; index++) {
            var current = text[index];
            if (index > 0 && char.IsUpper(current) && (char.IsLower(text[index - 1]) || char.IsDigit(text[index - 1]))) {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString().Replace("  ", " ", StringComparison.Ordinal).Trim();
    }
}
