using NINA.Core.Utility;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Serialization;
using NINA.ViewModel.Sequencer;
using NINA.WPF.Base.ViewModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NinaSequenceDiff;

/// <summary>
/// Provides the dockable Imaging panel for Sequencer Item Recovery.
///
/// The panel keeps a baseline of a sequence loaded by N.I.N.A. or an unsaved
/// in-memory sequence, compares it with the edited Advanced Sequencer tree,
/// and offers a Restore button only for complete items that were removed.
/// </summary>
[Export(typeof(IDockableVM))]
public sealed class SequenceDiffDockableVM : DockableVM, IDisposable {
    // A damaged or entirely replaced sequence could otherwise produce thousands
    // of rows every second. This keeps the panel responsive and predictable.
    private const int MaximumRemovedItems = 1_000;

    // N.I.N.A. gives plugins this mediator so that they can read and publish
    // the live Advanced Sequencer tree.
    private readonly ISequenceMediator sequenceMediator;

    // The timer watches the open sequence once per second, even if the panel
    // is hidden. It never changes the sequence by itself.
    private readonly DispatcherTimer refreshTimer;

    // A comparison result has no timestamp in JSON. Keep the first time that a
    // stable change key is observed so the table can show when it disappeared.
    private readonly Dictionary<string, DateTime> changeTimes =
        new(StringComparer.Ordinal);

    // N.I.N.A. stores every container's instruction list in an observable
    // collection. Listening to those collections closes the one-second gap in
    // which a newly added instruction could otherwise be deleted before the
    // timer had saved it. The set also lets us unsubscribe cleanly when the
    // user opens another sequence.
    private readonly HashSet<INotifyCollectionChanged>
        observedItemCollections = new();

    // A collection event provides the removed live object and its exact former
    // position. Keep that evidence until Restore or a deliberate new baseline
    // clears it. This fallback is independent of JSON array alignment.
    private readonly Dictionary<ISequenceItem, SequenceDifference>
        eventRemovedItems = new(ReferenceEqualityComparer.Instance);

    // The current root object identifies which open sequence this baseline
    // belongs to. A different root means the user loaded or created another
    // sequence, so a new baseline is needed.
    private ISequenceRootContainer? observedRoot;
    private string? baselineJson;
    // Every open sequence also has a short-lived working snapshot. It notices
    // deleted items added after loading and is the primary source for a new,
    // unsaved sequence.
    private InMemorySequenceBaseline? inMemoryBaseline;
    private bool primaryBaselineFollowsInMemory;

    // Restore itself inserts an item and therefore raises CollectionChanged.
    // Suppress the event-driven refresh during that controlled insertion so
    // the existing multi-item Restore logic can update its baseline as one
    // complete operation.
    private bool suppressCollectionRefresh;
    // A repeating reflection or serialisation error would otherwise be written
    // once per second. Remember it until a successful comparison clears it.
    private string? loggedComparisonFailure;
    private string statusText =
        "Sequencer Item Recovery is inactive: open an Advanced Sequencer " +
        "sequence to start comparing.";
    private string baselineText = "No Advanced Sequencer baseline yet";

    /// <summary>
    /// Lets MEF construct the panel with N.I.N.A.'s profile and sequencer
    /// services. It also creates the visible commands and the one-second timer.
    /// </summary>
    [ImportingConstructor]
    public SequenceDiffDockableVM(
        IProfileService profileService,
        ISequenceMediator sequenceMediator)
        : base(profileService) {
        this.sequenceMediator = sequenceMediator;
        Title = "Sequencer Item Recovery";
        ImageGeometry = CreateIcon();
        RefreshCommand = new DelegateCommand(_ => {
            // Record the explicit button action separately from the regular
            // background checks. This makes a later problem report easier to
            // follow without logging the sequence JSON itself.
            Logger.Info("Sequencer Item Recovery: manual Compare now requested.");
            Refresh();
        });
        SetBaselineCommand = new DelegateCommand(
            _ => SetBaselineFromCurrent());

        // ICollectionView adds sorting without changing the original
        // collection.
        // The newest detected deletion must be at the top when the panel opens.
        RemovedItemView = CollectionViewSource.GetDefaultView(RemovedItems);
        RemovedItemView.SortDescriptions.Add(
            new SortDescription(
                nameof(SequenceDifference.ChangedAt),
                ListSortDirection.Descending));

        // Normal priority ensures that an added item is observed between two
        // ordinary Sequencer edits. Background priority can be starved while
        // N.I.N.A. is laying out its editor, losing a short recovery window.
        refreshTimer = new DispatcherTimer(DispatcherPriority.Normal) {
            Interval = TimeSpan.FromSeconds(1)
        };
        // Background observation is necessary for recovery without a manual
        // Compare click: a deletion may happen while the Imaging panel is shut.
        refreshTimer.Tick += (_, _) => Refresh();
        refreshTimer.Start();
    }

    // The observable list notifies WPF when rows are added, removed or rebuilt.
    public ObservableCollection<SequenceDifference> RemovedItems { get; } =
        new();

    /// <summary>
    /// Marks this dockable view as a tool rather than a passive Imaging status
    /// panel. N.I.N.A. consequently keeps its four-square toggle in the
    /// upper-right Tools toolbar even after the panel has been closed.
    /// </summary>
    public override bool IsTool => true;

    // This sorted view is bound to the table. The original list remains useful
    // for counting and reconstructing rows.
    public ICollectionView RemovedItemView { get; }

    // WPF invokes these commands when the two buttons above the table are used.
    public ICommand RefreshCommand { get; }

    public ICommand SetBaselineCommand { get; }

    // Binding-friendly status text. RaisePropertyChanged tells WPF to redraw
    // it.
    public string StatusText {
        get => statusText;
        private set {
            if (statusText == value) {
                return;
            }

            statusText = value;
            RaisePropertyChanged();
        }
    }

    // This tells the user whether the baseline came from a saved file or from
    // the sequence currently in memory.
    public string BaselineText {
        get => baselineText;
        private set {
            if (baselineText == value) {
                return;
            }

            baselineText = value;
            RaisePropertyChanged();
        }
    }

    /// <summary>
    /// Stops the timer when N.I.N.A. disposes this dockable view. This prevents
    /// a closed plugin from continuing to schedule comparison work.
    /// </summary>
    public void Dispose() {
        refreshTimer.Stop();
        ResetItemCollectionSubscriptions();
    }

    /// <summary>
    /// Obtains the current tree, captures a baseline for a loaded or unsaved
    /// sequence, and rebuilds the list of deleted instructions. It never
    /// writes to the sequence.
    /// </summary>
    private void Refresh() {
        try {
            var sequenceViewModel = TryGetSequenceViewModel();
            var root = sequenceViewModel?.Sequencer?.MainContainer;
            if (sequenceViewModel is null || root is null) {
                ShowWaitingState();
                return;
            }

            // A new root is a new sequence. Capture its saved source or its
            // first in-memory state before comparing later edits.
            if (!ReferenceEquals(root, observedRoot)) {
                CaptureBaselineForCurrentSequence(sequenceViewModel, root);
            }

            // Newly inserted containers bring their own Items collections.
            // Synchronising on every refresh keeps the watcher aligned with
            // the live tree without retaining removed containers in memory.
            SynchronizeItemCollectionSubscriptions(root);

            if (baselineJson is null) {
                ShowWaitingState();
                return;
            }

            var currentJson = Serialize(sequenceViewModel);
            var primaryDifferences = SequenceJsonDiffer
                .CompareRemovedSequencerItems(
                    baselineJson,
                    currentJson,
                    MaximumRemovedItems)
                .ToArray();
            SetRestoreBaseline(primaryDifferences, baselineJson);

            // The working snapshot starts from what N.I.N.A. currently shows.
            // This covers a new unsaved sequence and items added after a JSON
            // sequence was loaded. Its JSON must be saved before it advances.
            var workingBaselineJson = inMemoryBaseline?.Json;
            var workingDifferences = workingBaselineJson is null
                ? Array.Empty<SequenceDifference>()
                : SequenceJsonDiffer
                    .CompareRemovedSequencerItems(
                        workingBaselineJson,
                        currentJson,
                        MaximumRemovedItems)
                    .ToArray();
            SetRestoreBaseline(workingDifferences, workingBaselineJson);

            // Prefer the loaded or manually chosen baseline when both sources
            // identify the same path. It is the stronger reference, while the
            // working snapshot contributes only newly added deleted items.
            var differences = MergeRemovedItems(
                primaryDifferences,
                workingDifferences,
                MaximumRemovedItems);
            differences = MergeRemovedItems(
                differences,
                eventRemovedItems.Values,
                MaximumRemovedItems);

            // Until something disappears, the working snapshot follows normal
            // construction work automatically. After a deletion it freezes,
            // retaining the complete object needed by Restore.
            var workingSnapshotChanged = !string.IsNullOrWhiteSpace(
                    workingBaselineJson) &&
                !string.Equals(
                    workingBaselineJson,
                    currentJson,
                    StringComparison.Ordinal);
            var workingSnapshotAdvanced = inMemoryBaseline
                ?.AdvanceWhenNothingWasRemoved(
                    currentJson,
                    workingDifferences.Length > 0 ||
                    eventRemovedItems.Count > 0) == true;
            if (workingSnapshotAdvanced) {
                if (primaryBaselineFollowsInMemory) {
                    baselineJson = inMemoryBaseline!.Json;
                    BaselineText =
                        "Unsaved in-memory baseline; automatic recovery is " +
                        "watching in the background";
                }

                if (workingSnapshotChanged) {
                    // This records that the timer observed an addition or an
                    // ordinary edit, without writing any sequence JSON into
                    // N.I.N.A.'s shared support log.
                    Logger.Info(
                        "Sequencer Item Recovery: updated the in-memory " +
                        "snapshot after a non-deleting sequence change.");
                }
            }

            ReplaceRemovedItems(differences);

            StatusText = RemovedItems.Count switch {
                0 => "No removed Advanced Sequencer items compared with the " +
                     "current recovery baseline.",
                MaximumRemovedItems =>
                    $"At least {MaximumRemovedItems:N0} removed items " +
                    "(display limited).",
                1 => "1 removed Advanced Sequencer item can be restored.",
                _ => $"{RemovedItems.Count:N0} removed Advanced Sequencer " +
                     "items can be restored."
            };
            loggedComparisonFailure = null;
        } catch (Exception exception) {
            // Fail safely: an unreadable N.I.N.A. object tree must never leave
            // old Restore buttons available for a different sequence.
            ClearRemovedItems();
            StatusText = $"Comparison unavailable: {exception.Message}";
            var failureText = exception.ToString();
            if (!string.Equals(
                    loggedComparisonFailure,
                    failureText,
                    StringComparison.Ordinal)) {
                Logger.Error(
                    "Sequencer Item Recovery: comparison unavailable.",
                    exception);
                loggedComparisonFailure = failureText;
            }
        }
    }

    /// <summary>
    /// Accepts the current tree as a new baseline. The user chooses this after
    /// intentional edits, so previously detected removal rows are discarded.
    /// </summary>
    private void SetBaselineFromCurrent() {
        try {
            var sequenceViewModel = TryGetSequenceViewModel();
            var root = sequenceViewModel?.Sequencer?.MainContainer;
            if (sequenceViewModel is null || root is null) {
                ShowWaitingState();
                return;
            }

            baselineJson = Serialize(sequenceViewModel);
            // The manual choice remains a fixed reference. A separate working
            // snapshot still tracks future newly added items for recovery.
            inMemoryBaseline = new InMemorySequenceBaseline(baselineJson);
            primaryBaselineFollowsInMemory = false;
            observedRoot = root;
            SynchronizeItemCollectionSubscriptions(root);
            BaselineText = "Current sequence chosen as the comparison baseline";
            ClearRemovedItems();
            StatusText = "The current sequence is now the comparison baseline.";
            Logger.Info(
                "Sequencer Item Recovery: current sequence chosen as the " +
                "manual baseline.");
        } catch (Exception exception) {
            StatusText =
                $"Unable to set the comparison baseline: {exception.Message}";
        }
    }

    /// <summary>
    /// Restores one row by cloning the corresponding item from the baseline and
    /// inserting it into the live tree. It is the only method in this class
    /// that changes the Advanced Sequencer.
    /// </summary>
    private void RestoreRemovedItem(SequenceDifference difference) {
        try {
            if (sequenceMediator.IsAdvancedSequenceRunning()) {
                // N.I.N.A.'s public plugin API reports that a sequence runs,
                // but does not expose the lower-right Pause state. Ask the
                // person to confirm that they paused before changing the tree.
                var confirmation = MessageBox.Show(
                    "Sequencer Item Recovery cannot detect N.I.N.A.'s pause " +
                    "state. Use N.I.N.A.'s Pause control at the lower-right " +
                    "first. If the sequence is paused and it is safe to " +
                    "change it, choose Yes to restore this item. Otherwise " +
                    "choose No.",
                    "Restore while paused",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (confirmation != MessageBoxResult.Yes) {
                    StatusText =
                        "Restore not performed. Pause the sequence in " +
                        "N.I.N.A. before trying again.";
                    Logger.Info(
                        "Sequencer Item Recovery: Restore canceled because " +
                        "the pause confirmation was declined for " +
                        DescribeLogItem(difference) + ".");
                    return;
                }
            }

            Logger.Info(
                "Sequencer Item Recovery: Restore requested for " +
                DescribeLogItem(difference) + ".");

            var sequenceViewModel = TryGetSequenceViewModel();
            if (sequenceViewModel?.Sequencer?.MainContainer is null) {
                ShowWaitingState();
                return;
            }

            // Deserialize the complete baseline first. That gives N.I.N.A. a
            // coherent object graph from which the removed item can be cloned.
            var serializer = new SequenceJsonConverter(
                sequenceViewModel.SequencerFactory);
            var baselineRoot = serializer.Deserialize(
                difference.RestoreBaselineJson ?? baselineJson ?? string.Empty)
                as ISequenceRootContainer
                ?? throw new InvalidOperationException(
                    "N.I.N.A. could not read the recovery baseline.");
            var currentRoot = sequenceViewModel.Sequencer.MainContainer;

            suppressCollectionRefresh = true;
            try {
                SequenceJsonRestore.RestoreRemovedItem(
                    currentRoot,
                    baselineRoot,
                    difference);

                // Publishing the changed root asks N.I.N.A. to redraw its
                // visible Advanced Sequencer with the inserted instruction or
                // block.
                sequenceMediator.SetAdvancedSequence(currentRoot);
            } finally {
                suppressCollectionRefresh = false;
            }
            observedRoot = currentRoot;
            SynchronizeItemCollectionSubscriptions(currentRoot);

            // The restored path is no longer missing. Remove any event-based
            // fallback for the same position before rebuilding the table.
            foreach (var removedItem in eventRemovedItems
                         .Where(pair => string.Equals(
                             pair.Value.Path,
                             difference.Path,
                             StringComparison.Ordinal))
                         .Select(pair => pair.Key)
                         .ToArray()) {
                eventRemovedItems.Remove(removedItem);
            }

            // Keep the previous working snapshot while another deleted item
            // still needs it. Replacing that snapshot too early would remove
            // the other item's Restore row, because the new snapshot already
            // lacks it. Once every pending working-only deletion is resolved,
            // re-arm the snapshot so later additions are observed again.
            var restoredJson = Serialize(sequenceViewModel);
            var remainingWorkingDifferences = inMemoryBaseline is null
                ? Array.Empty<SequenceDifference>()
                : SequenceJsonDiffer.CompareRemovedSequencerItems(
                    inMemoryBaseline.Json,
                    restoredJson,
                    MaximumRemovedItems);
            var remainingRecoveries = MergeRemovedItems(
                remainingWorkingDifferences,
                eventRemovedItems.Values,
                MaximumRemovedItems);
            if (remainingRecoveries.Count == 0) {
                inMemoryBaseline = new InMemorySequenceBaseline(restoredJson);
                Logger.Info(
                    "Sequencer Item Recovery: re-armed the in-memory " +
                    "recovery snapshot after Restore.");
            } else {
                Logger.Info(
                    "Sequencer Item Recovery: kept the in-memory recovery " +
                    "snapshot for " +
                    $"{remainingRecoveries.Count} other removed " +
                    "item(s).");
            }

            Refresh();
            StatusText =
                $"Restored in the Advanced Sequencer: " +
                $"{difference.InstructionText}";
            Logger.Info(
                "Sequencer Item Recovery: Restore completed for " +
                DescribeLogItem(difference) + ".");
        } catch (Exception exception) {
            Logger.Error(exception);
            StatusText =
                $"Unable to restore the previous value: {exception.Message}";
        }
    }

    /// <summary>
    /// Saves a baseline for the current sequence. A loaded JSON file remains
    /// the authoritative reference; every sequence also receives a working
    /// in-memory snapshot so new instructions can be recovered automatically.
    /// </summary>
    private void CaptureBaselineForCurrentSequence(
        ISequence2VM sequenceViewModel,
        ISequenceRootContainer root) {
        ResetItemCollectionSubscriptions();
        observedRoot = root;
        var initialJson = Serialize(sequenceViewModel);
        inMemoryBaseline = new InMemorySequenceBaseline(initialJson);
        var savePath = TryGetSavePath(sequenceViewModel);
        if (!string.IsNullOrWhiteSpace(savePath) && File.Exists(savePath)) {
            baselineJson = File.ReadAllText(savePath);
            primaryBaselineFollowsInMemory = false;
            BaselineText = $"Last loaded: {Path.GetFileName(savePath)}";
        } else {
            baselineJson = inMemoryBaseline.Json;
            primaryBaselineFollowsInMemory = true;
            BaselineText =
                "Unsaved in-memory baseline; automatic recovery is watching " +
                "in the background";
        }

        ClearRemovedItems();
        Logger.Info(
            "Sequencer Item Recovery: captured a " +
            (primaryBaselineFollowsInMemory
                ? "new in-memory recovery baseline."
                : "loaded-sequence recovery baseline."));
    }

    /// <summary>
    /// Subscribes to every instruction collection in the current tree and
    /// removes subscriptions that no longer belong to it. Collection events
    /// are the reliable, immediate signal for quick add-then-delete actions;
    /// the timer remains responsible for ordinary property edits.
    /// </summary>
    private void SynchronizeItemCollectionSubscriptions(
        ISequenceContainer root) {
        var currentCollections = new HashSet<INotifyCollectionChanged>();
        CollectItemCollections(root, currentCollections);

        foreach (var oldCollection in observedItemCollections
                     .Except(currentCollections)
                     .ToArray()) {
            oldCollection.CollectionChanged -= OnItemsCollectionChanged;
            observedItemCollections.Remove(oldCollection);
        }

        foreach (var newCollection in currentCollections
                     .Except(observedItemCollections)
                     .ToArray()) {
            newCollection.CollectionChanged += OnItemsCollectionChanged;
            observedItemCollections.Add(newCollection);
        }
    }

    /// <summary>
    /// Walks only N.I.N.A.'s instruction hierarchy. Conditions and triggers
    /// are deliberately excluded because the panel restores instructions and
    /// blocks, not every kind of Sequencer object.
    /// </summary>
    private static void CollectItemCollections(
        ISequenceContainer container,
        ISet<INotifyCollectionChanged> destination) {
        if (container.Items is INotifyCollectionChanged collection) {
            destination.Add(collection);
        }

        foreach (var childContainer in container.Items
                     .OfType<ISequenceContainer>()) {
            CollectItemCollections(childContainer, destination);
        }
    }

    /// <summary>
    /// Compares immediately after N.I.N.A. adds or removes an instruction.
    /// This makes recovery independent of how quickly the user works and means
    /// the former two-second waiting recommendation is no longer required.
    /// </summary>
    private void OnItemsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs args) {
        if (suppressCollectionRefresh) {
            return;
        }

        // A normal move raises Remove followed by Add for the same object. The
        // Add half cancels the temporary recovery row. A genuinely deleted
        // object never returns and therefore remains available for Restore.
        if (args.NewItems is not null) {
            foreach (var addedItem in args.NewItems.OfType<ISequenceItem>()) {
                eventRemovedItems.Remove(addedItem);
            }
        }

        CaptureEventRemovedItems(sender, args);

        Refresh();
    }

    /// <summary>
    /// Converts N.I.N.A.'s exact collection-removal event into a recovery row.
    /// The baseline still contains the old object, while OldStartingIndex tells
    /// us its position in the collection immediately before deletion.
    /// </summary>
    private void CaptureEventRemovedItems(
        object? sender,
        NotifyCollectionChangedEventArgs args) {
        if (args.Action != NotifyCollectionChangedAction.Remove ||
            args.OldItems is null ||
            args.OldStartingIndex < 0 ||
            sender is not INotifyCollectionChanged collection ||
            observedRoot is null ||
            string.IsNullOrWhiteSpace(inMemoryBaseline?.Json)) {
            return;
        }

        var parentIndices = new List<int>();
        if (!TryFindItemCollectionPath(
                observedRoot,
                collection,
                parentIndices)) {
            return;
        }

        for (var offset = 0; offset < args.OldItems.Count; offset++) {
            if (args.OldItems[offset] is not ISequenceItem removedItem) {
                continue;
            }

            var indices = parentIndices
                .Append(args.OldStartingIndex + offset)
                .ToArray();
            var path = BuildJsonItemPath(indices);
            var difference = SequenceJsonDiffer
                .CreateRemovedSequencerItemAtPath(
                    inMemoryBaseline.Json,
                    path);
            if (difference is null) {
                continue;
            }

            difference.RestoreBaselineJson = inMemoryBaseline.Json;
            eventRemovedItems[removedItem] = difference;
        }
    }

    /// <summary>
    /// Finds the sequence of item indices leading from the root to the
    /// container that owns one observable Items collection.
    /// </summary>
    private static bool TryFindItemCollectionPath(
        ISequenceContainer container,
        INotifyCollectionChanged target,
        IList<int> indices) {
        if (ReferenceEquals(container.Items, target)) {
            return true;
        }

        for (var index = 0; index < container.Items.Count; index++) {
            if (container.Items[index] is not ISequenceContainer child) {
                continue;
            }

            indices.Add(index);
            if (TryFindItemCollectionPath(child, target, indices)) {
                return true;
            }

            indices.RemoveAt(indices.Count - 1);
        }

        return false;
    }

    /// <summary>
    /// Converts live-tree item indices to the JSON path notation used by the
    /// differ and Restore code. Every index descends through one Items list.
    /// </summary>
    internal static string BuildJsonItemPath(IEnumerable<int> indices) {
        var path = "$";
        foreach (var index in indices) {
            path += $".Items['$values'][{index}]";
        }

        return path;
    }

    /// <summary>
    /// Releases all observable collections before switching sequence roots or
    /// disposing the dockable tool. This prevents stale sequences from calling
    /// into the plugin later and prevents avoidable object retention.
    /// </summary>
    private void ResetItemCollectionSubscriptions() {
        foreach (var collection in observedItemCollections) {
            collection.CollectionChanged -= OnItemsCollectionChanged;
        }

        observedItemCollections.Clear();
    }

    /// <summary>
    /// Marks every comparison result with the complete JSON tree that created
    /// it. This is essential for restoring a newly added item that did not
    /// exist in the originally loaded file.
    /// </summary>
    internal static void SetRestoreBaseline(
        IEnumerable<SequenceDifference> differences,
        string? sourceJson) {
        if (string.IsNullOrWhiteSpace(sourceJson)) {
            return;
        }

        foreach (var difference in differences) {
            difference.RestoreBaselineJson = sourceJson;
        }
    }

    /// <summary>
    /// Combines removals from the fixed baseline and working snapshot without
    /// showing a duplicate row for the same original location. The fixed
    /// baseline wins because it represents an explicitly loaded or chosen
    /// sequence state.
    /// </summary>
    private static IReadOnlyList<SequenceDifference> MergeRemovedItems(
        IEnumerable<SequenceDifference> primaryDifferences,
        IEnumerable<SequenceDifference> workingDifferences,
        int maximumItems) {
        var combined = primaryDifferences.ToList();
        var knownPaths = new HashSet<string>(
            combined.Select(difference => difference.Path),
            StringComparer.Ordinal);

        foreach (var difference in workingDifferences) {
            if (knownPaths.Add(difference.Path)) {
                combined.Add(difference);
            }
        }

        return combined.Take(maximumItems).ToArray();
    }

    /// <summary>
    /// Finds N.I.N.A.'s editable Advanced Sequencer view model. The public
    /// plugin interface does not expose it directly in N.I.N.A. 3.2, so the
    /// short reflection bridge below is intentionally the only such bridge.
    /// </summary>
    private ISequence2VM? TryGetSequenceViewModel() {
        if (!sequenceMediator.Initialized) {
            return null;
        }

        /*
         * N.I.N.A. keeps the navigation view model in this private field.
         * Reflection is restricted to this read-only lookup. If a future
         * version changes the field, the panel reports that it is inactive;
         * it never guesses a replacement or makes a file-only change.
         */
        var navigationField = sequenceMediator.GetType().GetField(
            "sequenceNavigation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var navigation = navigationField?.GetValue(sequenceMediator)
            as ISequenceNavigationVM;
        return navigation?.Sequence2VM;
    }

    // SavePath is a public property on the concrete N.I.N.A. view model, but
    // not on its interface. Reflection avoids coupling this plugin to it.
    private static string TryGetSavePath(ISequence2VM sequenceViewModel) {
        var savePathProperty = sequenceViewModel.GetType().GetProperty(
            "SavePath",
            BindingFlags.Instance | BindingFlags.Public);
        return savePathProperty?.GetValue(sequenceViewModel) as string
            ?? string.Empty;
    }

    // Use N.I.N.A.'s converter instead of a general JSON serializer. It knows
    // the sequence types and their reference handling.
    private static string Serialize(ISequence2VM sequenceViewModel) {
        var serializer = new SequenceJsonConverter(
            sequenceViewModel.SequencerFactory);
        return serializer.Serialize(sequenceViewModel.Sequencer.MainContainer);
    }

    /// <summary>
    /// Replaces the visible rows while retaining the first-detection time for
    /// each still-removed item. A vanished key means the item was restored or
    /// the baseline changed, so its saved time can be forgotten.
    /// </summary>
    private void ReplaceRemovedItems(
        IEnumerable<SequenceDifference> differences) {
        var incoming = differences.ToArray();
        var activeChangeKeys = new HashSet<string>(
            incoming.Select(difference => difference.ChangeKey),
            StringComparer.Ordinal);

        foreach (var staleKey in changeTimes.Keys
                     .Where(key => !activeChangeKeys.Contains(key))
                     .ToArray()) {
            changeTimes.Remove(staleKey);
        }

        RemovedItems.Clear();
        foreach (var difference in incoming) {
            if (!changeTimes.TryGetValue(
                    difference.ChangeKey,
                    out var changedAt)) {
                changedAt = DateTime.Now;
                changeTimes.Add(difference.ChangeKey, changedAt);
                Logger.Info(
                    "Sequencer Item Recovery: detected removed item: " +
                    DescribeLogItem(difference) + ".");
            }

            difference.ChangedAt = changedAt;

            // Only an actual deleted sequencer item receives a Restore command.
            // Added or changed values cannot become actionable by accident.
            if (!string.IsNullOrWhiteSpace(difference.BeforeJson)) {
                difference.RestoreCommand = new DelegateCommand(
                    _ => RestoreRemovedItem(difference));
            }

            RemovedItems.Add(difference);
        }
    }

    // Clearing both collections prevents timestamps from a previous sequence
    // from appearing beside a new baseline.
    private void ClearRemovedItems() {
        RemovedItems.Clear();
        changeTimes.Clear();
        eventRemovedItems.Clear();
    }

    // Keep a support log useful but privacy-conscious: the visible instruction
    // summary helps identify the event, while JSON, disk paths and settings
    // remain out of the shared N.I.N.A. log.
    private static string DescribeLogItem(SequenceDifference difference) =>
        string.IsNullOrWhiteSpace(difference.InstructionText)
            ? "an unnamed Advanced Sequencer item"
            : $"'{difference.InstructionText}'";

    // The inactive state is a normal wait for N.I.N.A. to expose an Advanced
    // Sequencer tree. It does not mean that a deleted item has been lost.
    private void ShowWaitingState() {
        ClearRemovedItems();
        StatusText =
            "Sequencer Item Recovery is inactive: open Sequencer, select " +
            "Advanced, then load or create a sequence.";
        BaselineText = "No Advanced Sequencer baseline yet";
    }

    // The four small rectangles are the icon shown in N.I.N.A.'s Tools toolbar.
    private static GeometryGroup CreateIcon() {
        var geometry = Geometry.Parse(
            "M3,3 H11 V11 H3 Z M13,3 H21 V11 H13 Z " +
            "M3,13 H11 V21 H3 Z M13,13 H21 V21 H13 Z");
        var group = new GeometryGroup();
        group.Children.Add(geometry);
        group.Freeze();
        return group;
    }

    /// <summary>
    /// Minimal ICommand adapter used for buttons in this plugin. WPF calls
    /// Execute when a button is pressed; the optional predicate can disable a
    /// command when a future use case needs it.
    /// </summary>
    private sealed class DelegateCommand : ICommand {
        private readonly Action<object?> execute;
        private readonly Func<object?, bool>? canExecute;

        public DelegateCommand(
            Action<object?> execute,
            Func<object?, bool>? canExecute = null) {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        // The current commands are always available. The empty event accessors
        // fulfil ICommand without maintaining an unnecessary event list.
        public event EventHandler? CanExecuteChanged {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) =>
            canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
