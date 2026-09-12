# Sequencer Item Recovery for N.I.N.A.

Local N.I.N.A. plugin for the **Advanced Sequencer** view. Sequencer Item
Recovery is a short-term recovery tool: it notices items removed from an open
sequence soon after the deletion, so they can be restored before the sequence
is saved. It is not a full undo history or an automatic backup.

## User interface

### Finding Sequencer Item Recovery

Load or create a sequence in **Sequencer → Advanced**, then select **Imaging**
in N.I.N.A.'s main navigation. Sequencer Item Recovery is a **tool panel** in
the Imaging workspace. Its four-square icon is in the **Tools** group at the
upper-right edge; click it to show or hide Sequencer Item Recovery. The icon
remains available after the panel is closed. The panel is dockable and its
position is stored with the active N.I.N.A. profile, so it can appear on any
edge or as a floating panel.

The **Plugins → Installed → Sequencer Item Recovery** page now contains the
same complete, in-app guide. This is the place to read the purpose, normal
workflow, limits, and recovery rules without leaving N.I.N.A.

### Reading the removed-instructions list

The table is in English and lists only deleted Advanced Sequencer instructions
or blocks:

- **Removed at** — local first-detection time, formatted `yyyy-MM-dd HH:mm:ss`.
  The newest deletion is shown first by default.
- **Instruction** — the same visible item name shown by the Sequencer, plus the
  most useful settings (for example, exposure time and filter). It never shows
  JSON or internal type names.
- **Action** — a visible **'Restore'** button.

There are no hover-only details or right-click actions. Click a column heading
to sort the timestamp or instruction name.

### Restoring an item

Click **'Restore'** in the row for the deleted item. Sequencer Item Recovery
inserts the full item at its original position in the in-memory sequence.
Inspect the result in the Advanced Sequencer and save normally in N.I.N.A. when
you want to keep it.

If a deletion happens during a run, use N.I.N.A.'s lower-right **'Pause'**
control first; do not cancel the sequence. Then use **'Restore'** and confirm
that the sequence is paused. Sequencer Item Recovery cannot inspect N.I.N.A.'s
pause state, so it requires that explicit safety confirmation before changing
a running sequence. Review the result and use N.I.N.A.'s resume control to
continue.

## Compatibility

This project targets the locally installed **N.I.N.A. 3.2.0.9001** API and
requires .NET 8. It is local-only and is not published automatically.

N.I.N.A. supplies a plugin with the sequence save path but not the editable
Advanced Sequencer tree. For N.I.N.A. 3.2 this plugin uses a contained,
defensive reflection bridge to the existing `ISequenceMediator`. If a future
N.I.N.A. version changes that bridge, the panel reports the problem and never
modifies the sequence.

## License and source

Copyright 2026 Jost Jahn. This project is licensed under the Mozilla Public
License, version 2.0 (MPL-2.0); see [LICENSE](LICENSE) for the complete text.

The public source repository is
<https://github.com/JostJahn/nina-sequencer-item-recovery>. Source publication
does not make any local working ZIP a public release; release downloads are
published separately after artifact review.

## AI assistance and accountability

AI assistance was used while drafting and structuring this project, including
spelling, translation, formatting, source comments and documentation. Jost
Jahn is the accountable human maintainer and reviews the source and each
release artifact before publication.

## Use

1. Load a JSON sequence in the Advanced Sequencer.
2. In **Imaging**, open the **Sequencer Item Recovery** tool from the
   upper-right **Tools** toolbar.
3. The table refreshes every second. Use **'Compare now'** for an immediate
   refresh. It does not change the baseline or discard an existing
   **'Restore'** row. Use **'Use current sequence as baseline'** only after an
   intentional set of edits and only when no row still needs restoring: it
   accepts the current tree as the new reference and clears every current row.

For a saved sequence, the unchanged file content is the baseline. The plugin
also watches the open Advanced Sequencer automatically about once per second,
including while its Imaging panel is hidden. For a new sequence without a save
path, the working in-memory snapshot follows normal additions until a deletion
is detected. This lets you compose a new sequence and recover an item removed
by mistake without clicking **'Compare now'** first. After a deletion is
detected, its last complete in-memory state is retained for recovery. Runtime
and object-reference fields, such as status and `$id`, are ignored.

Additions and deletions are watched immediately, so a new instruction does not
need to remain in place for two seconds before it can be recovered. A
once-per-second background check also follows changes to item settings; no
**'Compare now'** click is needed.

When no removed items are listed, **'Compare now'** only performs the usual
automatic check immediately. **'Use current sequence as baseline'** then
starts a new reference for later comparisons, but it does not create a backup
or an undo history. Restoring one item leaves any other still-removed items
available through their own **'Restore'** rows.

If Sequencer Item Recovery reports **Inactive**, N.I.N.A. has not yet exposed
an Advanced Sequencer tree. Open **Sequencer**, choose **Advanced**, then load
or create a sequence. Sequencer Item Recovery becomes active automatically; no
equipment connection or sequence start is required.

## Build and test

```powershell
dotnet test .\tests\NinaSequenceDiff.Tests\NinaSequenceDiff.Tests.csproj
dotnet build .\src\NinaSequenceDiff\NinaSequenceDiff.csproj -c Release
```

The release DLL is placed at
`src\NinaSequenceDiff\bin\Release\net8.0-windows\SequencerItemRecovery.dll`.

For a manual test, N.I.N.A. must be closed before updating the installed DLL.
Copy `SequencerItemRecovery.dll` into
`%LOCALAPPDATA%\NINA\Plugins\3.0.0\SequencerItemRecovery\`, then start N.I.N.A.
again. Do not keep an earlier preview in `NinaSequenceDiff` beside this folder:
both DLLs have the same stable plugin GUID.

## Limits

The panel restores only deletions detected while this sequence remains open and
compared with its baseline. If a deletion has already been saved, or the
sequence was closed before Sequencer Item Recovery detected it, you still need
an earlier JSON file or Windows File History.
