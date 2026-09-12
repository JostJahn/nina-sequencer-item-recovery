using System.Collections.Specialized;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem.Utility;

namespace NinaSequenceDiff.Tests;

public sealed class SequenceJsonDifferTests {
    [Fact]
    public void Compare_finds_a_real_nina_wait_instruction_after_removal() {
        // This integration-style test uses N.I.N.A.'s actual Wait instruction
        // and JSON converter. It protects against a realistic deletion being
        // hidden by differences between hand-written test JSON and N.I.N.A.'s
        // serialized object graph.
        var root = new SequenceRootContainer();
        var parent = new SequentialContainer { Name = "Test instructions" };
        var wait = new WaitForTimeSpan();
        root.Add(parent);
        parent.Add(wait);

        // These are the exact settings used by N.I.N.A.'s sequence converter
        // when it serializes the live tree. Its factory is needed only by the
        // separate deserialization path, so the test calls Newtonsoft directly.
        var settings = new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.All,
            PreserveReferencesHandling = PreserveReferencesHandling.All
        };
        var before = JsonConvert.SerializeObject(root, settings);
        Assert.True(parent.Remove(wait));
        var after = JsonConvert.SerializeObject(root, settings);

        var removal = Assert.Single(
            SequenceJsonDiffer.CompareRemovedSequencerItems(before, after));
        var eventPath = SequenceDiffDockableVM.BuildJsonItemPath(
            new[] { 0, 0 });
        var eventRemoval = SequenceJsonDiffer
            .CreateRemovedSequencerItemAtPath(before, eventPath);

        Assert.Equal("Wait For Time Span", removal.InstructionText);
        Assert.True(removal.IsRemovedSequencerItem);
        Assert.NotNull(eventRemoval);
        Assert.Equal("Wait For Time Span", eventRemoval.InstructionText);
        Assert.Equal(eventPath, eventRemoval.Path);
    }

    [Fact]
    public void Plugin_manifest_targets_the_installed_nina_version() {
        var plugin = new SequenceDiffPlugin();

        Assert.Equal("Sequencer Item Recovery", plugin.Name);
        Assert.Equal("3.2.0.9001", plugin.MinimumApplicationVersion.ToString());
    }

    [Fact]
    public void Compare_reports_a_changed_property() {
        const string before = """{ "Name": "M42", "Exposure": 120 }""";
        const string after = """{ "Name": "M42", "Exposure": 180 }""";

        var differences = SequenceJsonDiffer.Compare(before, after);

        var difference = Assert.Single(differences);
        Assert.Equal(SequenceDifferenceKind.Changed, difference.Kind);
        Assert.Equal("$.Exposure", difference.Path);
        Assert.Equal("120", difference.Before);
        Assert.Equal("180", difference.After);
    }

    [Fact]
    public void Compare_uses_the_visible_sequencer_instruction_in_value_tooltips() {
        const string before = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": {
                "$id": "2",
                "$values": [
                  {
                    "$id": "3",
                    "$type": "NINA.Sequencer.SequenceItem.Imaging.TakeExposure, NINA",
                    "ExposureTime": 120,
                    "Name": "Take Exposure",
                    "Parent": { "$ref": "1" }
                  }
                ]
              },
              "Parent": null
            }
            """;
        var after = before.Replace("\"ExposureTime\": 120", "\"ExposureTime\": 180", StringComparison.Ordinal);

        var difference = Assert.Single(SequenceJsonDiffer.Compare(before, after));

        Assert.Equal("120", difference.Before);
        Assert.Equal("180", difference.After);
        Assert.Contains("Sequence item: Take Exposure", difference.BeforeToolTip, StringComparison.Ordinal);
        Assert.DoesNotContain("NINA.Sequencer", difference.BeforeToolTip, StringComparison.Ordinal);
        Assert.DoesNotContain("$type", difference.AfterToolTip, StringComparison.Ordinal);
        Assert.Contains("Exposure Time", difference.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_ignores_runtime_and_reference_properties() {
        const string before = """{ "$id": "1", "Status": "CREATED", "Name": "M42" }""";
        const string after = """{ "$id": "9", "Status": "RUNNING", "Name": "M42" }""";

        var differences = SequenceJsonDiffer.Compare(before, after);

        Assert.Empty(differences);
    }

    [Fact]
    public void Compare_reports_one_removed_sequence_entity_without_index_cascade() {
        const string before = """
            {
              "Items": [
                { "$type": "Wait", "Name": "First" },
                { "$type": "Exposure", "Name": "Deleted" },
                { "$type": "Wait", "Name": "Last" }
              ]
            }
            """;
        const string after = """
            {
              "Items": [
                { "$type": "Wait", "Name": "First" },
                { "$type": "Wait", "Name": "Last" }
              ]
            }
            """;

        var differences = SequenceJsonDiffer.Compare(before, after);

        var difference = Assert.Single(differences);
        Assert.Equal(SequenceDifferenceKind.Removed, difference.Kind);
        Assert.Equal("$.Items[1]", difference.Path);
        Assert.Contains("Deleted", difference.Before, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_removed_sequencer_items_shows_only_a_deleted_instruction_with_its_settings() {
        const string before = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": {
                "$id": "2",
                "$values": [
                  {
                    "$id": "3",
                    "$type": "NINA.Sequencer.SequenceItem.Imaging.TakeExposure, NINA",
                    "Name": "Take Exposure",
                    "ExposureTime": 120,
                    "FilterName": "L",
                    "Parent": { "$ref": "1" }
                  },
                  {
                    "$id": "4",
                    "$type": "NINA.Sequencer.SequenceItem.Guider.Dither, NINA",
                    "Name": "Dither",
                    "Parent": { "$ref": "1" }
                  }
                ]
              },
              "Parent": null
            }
            """;
        const string after = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": {
                "$id": "2",
                "$values": [
                  {
                    "$id": "3",
                    "$type": "NINA.Sequencer.SequenceItem.Imaging.TakeExposure, NINA",
                    "Name": "Take Exposure",
                    "ExposureTime": 180,
                    "FilterName": "L",
                    "Parent": { "$ref": "1" }
                  }
                ]
              },
              "Parent": null
            }
            """;

        var difference = Assert.Single(SequenceJsonDiffer.CompareRemovedSequencerItems(before, after, maximumItems: 1));

        Assert.True(difference.IsRemovedSequencerItem);
        Assert.Equal("Dither", difference.InstructionText);
        Assert.Equal(string.Empty, difference.InstructionDetails);
        Assert.Equal(SequenceDifferenceKind.Removed, difference.Kind);
    }

    [Fact]
    public void Compare_removed_sequencer_items_includes_human_readable_settings() {
        const string before = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": {
                "$id": "2",
                "$values": [
                  {
                    "$id": "3",
                    "$type": "NINA.Sequencer.SequenceItem.Imaging.TakeExposure, NINA",
                    "Name": "Take Exposure",
                    "ExposureTime": 120,
                    "FilterName": "L",
                    "Parent": { "$ref": "1" }
                  }
                ]
              },
              "Parent": null
            }
            """;
        const string after = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": { "$id": "2", "$values": [] },
              "Parent": null
            }
            """;

        var difference = Assert.Single(SequenceJsonDiffer.CompareRemovedSequencerItems(before, after));

        Assert.Equal("Take Exposure", difference.InstructionText);
        Assert.Contains("Exposure Time: 120", difference.InstructionDetails, StringComparison.Ordinal);
        Assert.Contains("Filter Name: L", difference.InstructionDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("NINA.Sequencer", difference.InstructionDetails, StringComparison.Ordinal);
        Assert.Equal(new[] { 0 }, SequenceJsonRestore.GetItemIndices(difference.Path));
    }

    [Fact]
    public void Restore_reinserts_a_removed_instruction_with_valid_parent_reference() {
        const string before = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": {
                "$id": "2",
                "$values": [
                  {
                    "$id": "3",
                    "$type": "NINA.Sequencer.SequenceItem.Guider.Dither, NINA",
                    "Name": "Dither",
                    "Parent": { "$ref": "1" }
                  }
                ]
              },
              "Parent": null
            }
            """;
        const string after = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": { "$id": "2", "$values": [] },
              "Parent": null
            }
            """;
        var difference = Assert.Single(SequenceJsonDiffer.Compare(before, after));

        var restored = JObject.Parse(SequenceJsonRestore.Restore(after, difference));
        var item = (JObject)restored["Items"]!["$values"]![0]!;
        var ids = restored.SelectTokens("$..$id").Select(token => token.Value<string>()).ToArray();

        Assert.Equal("Dither", item["Name"]!.Value<string>());
        Assert.Equal(restored["$id"]!.Value<string>(), item["Parent"]!["$ref"]!.Value<string>());
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Restore_removed_sequencer_item_clones_the_baseline_item_into_the_live_tree() {
        var baselineRoot = new SequenceRootContainer();
        var baselineParent = new SequentialContainer { Name = "Target instructions" };
        var removedBlock = new SequentialContainer { Name = "Deleted block" };
        baselineRoot.Add(baselineParent);
        baselineParent.Add(removedBlock);

        var currentRoot = new SequenceRootContainer();
        var currentParent = new SequentialContainer { Name = "Target instructions" };
        currentRoot.Add(currentParent);

        var difference = new SequenceDifference(
            SequenceDifferenceKind.Removed,
            "$.Items.$values[0].Items.$values[0]",
            "Target instructions",
            "Deleted block",
            string.Empty,
            "Deleted block",
            string.Empty,
            "{}",
            null,
            true,
            "Deleted block",
            string.Empty);

        SequenceJsonRestore.RestoreRemovedItem(currentRoot, baselineRoot, difference);

        var restoredBlock = Assert.IsType<SequentialContainer>(Assert.Single(currentParent.Items));
        Assert.NotSame(removedBlock, restoredBlock);
        Assert.Equal("Deleted block", restoredBlock.Name);
        Assert.Same(currentParent, restoredBlock.Parent);
    }

    [Fact]
    public void Restore_accepts_a_committed_insert_when_a_ui_observer_throws() {
        // ObservableCollection inserts first and notifies observers second.
        // This test reproduces the WPF failure seen in N.I.N.A.: the observer
        // throws after the instruction is already part of the live sequence.
        var baselineRoot = new SequenceRootContainer();
        var baselineParent = new SequentialContainer {
            Name = "Target instructions"
        };
        var removedItem = new WaitForTimeSpan();
        baselineRoot.Add(baselineParent);
        baselineParent.Add(removedItem);

        var currentRoot = new SequenceRootContainer();
        var currentParent = new SequentialContainer {
            Name = "Target instructions"
        };
        currentRoot.Add(currentParent);

        var difference = new SequenceDifference(
            SequenceDifferenceKind.Removed,
            "$.Items.$values[0].Items.$values[0]",
            "Target instructions",
            "Wait For Time Span",
            string.Empty,
            "Wait For Time Span",
            string.Empty,
            "{}",
            null,
            true,
            "Wait For Time Span",
            string.Empty);
        var observableItems = Assert.IsAssignableFrom<
            INotifyCollectionChanged>(currentParent.Items);
        observableItems.CollectionChanged += (_, _) =>
            throw new ArgumentException(
                "Width and Height must be non-negative.");

        SequenceJsonRestore.RestoreRemovedItem(
            currentRoot,
            baselineRoot,
            difference);

        var restoredItem = Assert.IsType<WaitForTimeSpan>(
            Assert.Single(currentParent.Items));
        Assert.NotSame(removedItem, restoredItem);
        Assert.Same(currentParent, restoredItem.Parent);
    }

    [Fact]
    public void In_memory_baseline_follows_new_items_until_a_deletion_is_detected() {
        const string initial = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": {
                "$id": "2",
                "$values": [
                  {
                    "$id": "3",
                    "$type": "NINA.Sequencer.SequenceItem.Guider.Dither, NINA",
                    "Name": "First dither",
                    "Parent": { "$ref": "1" }
                  }
                ]
              },
              "Parent": null
            }
            """;
        const string afterAddingItem = """
            {
              "$id": "1",
              "$type": "NINA.Sequencer.Container.SequentialContainer, NINA",
              "Items": {
                "$id": "2",
                "$values": [
                  {
                    "$id": "3",
                    "$type": "NINA.Sequencer.SequenceItem.Guider.Dither, NINA",
                    "Name": "First dither",
                    "Parent": { "$ref": "1" }
                  },
                  {
                    "$id": "4",
                    "$type": "NINA.Sequencer.SequenceItem.Guider.Dither, NINA",
                    "Name": "New dither",
                    "Parent": { "$ref": "1" }
                  }
                ]
              },
              "Parent": null
            }
            """;

        // This mirrors the background timer while a person builds a new,
        // never-saved sequence. Adding an instruction advances the in-memory
        // recovery snapshot without requiring a Compare-button click.
        var baseline = new InMemorySequenceBaseline(initial);
        var additions = SequenceJsonDiffer.CompareRemovedSequencerItems(
            baseline.Json,
            afterAddingItem);
        Assert.Empty(additions);
        Assert.True(baseline.AdvanceWhenNothingWasRemoved(
            afterAddingItem,
            hasRemovedItems: false));

        // The next state is missing the newly added instruction. The helper
        // must keep the prior complete snapshot, enabling that instruction to
        // be found and restored just like one from a loaded JSON file.
        var deletions = SequenceJsonDiffer.CompareRemovedSequencerItems(
            baseline.Json,
            initial);
        var deletion = Assert.Single(deletions);
        Assert.Equal("New dither", deletion.InstructionText);
        SequenceDiffDockableVM.SetRestoreBaseline(
            deletions,
            afterAddingItem);
        Assert.Equal(afterAddingItem, deletion.RestoreBaselineJson);
        Assert.False(baseline.AdvanceWhenNothingWasRemoved(
            initial,
            hasRemovedItems: true));
        Assert.Equal(afterAddingItem, baseline.Json);
    }

    [Fact]
    public void In_memory_baseline_rearms_after_a_restored_item() {
        // A later restore returns the live sequence to a complete state. The
        // next scan must then be allowed to follow further additions, so a
        // second accidental deletion is recoverable without Compare.
        var baseline = new InMemorySequenceBaseline("\"first complete state\"");
        Assert.False(baseline.AdvanceWhenNothingWasRemoved(
            "\"item deleted\"",
            hasRemovedItems: true));
        Assert.Equal("\"first complete state\"", baseline.Json);

        Assert.True(baseline.AdvanceWhenNothingWasRemoved(
            "\"first complete state\"",
            hasRemovedItems: false));
        Assert.True(baseline.AdvanceWhenNothingWasRemoved(
            "\"first state plus another new item\"",
            hasRemovedItems: false));
        Assert.False(baseline.AdvanceWhenNothingWasRemoved(
            "\"first complete state\"",
            hasRemovedItems: true));
        Assert.Equal("\"first state plus another new item\"", baseline.Json);
    }
}
