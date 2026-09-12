# Sequencer Item Recovery 0.1.11.0 — draft release notes

Status: not published. Jost Jahn must complete the human-review record before
this text is used for a public release.

## Highlights

- Detects recently removed Advanced Sequencer instructions and blocks.
- Restores one removed item at its original position in the open sequence.
- Shows readable Sequencer-facing instruction details instead of JSON.
- Uses N.I.N.A.'s lower-right `'Pause'` control for safe recovery during a
  running sequence; it never instructs the user to cancel the sequence.
- Renamed to **Sequencer Item Recovery** to describe the recovery of one
  instruction or block, not an entire sequence.
- Watches the open Advanced Sequencer in the background, so no manual
  `'Compare now'` click is needed to protect a new, unsaved sequence.
- Retains a working snapshot for every open sequence. This also restores a
  newly added item that is deleted before the sequence is saved.
- Watches additions and removals immediately; no two-second delay or manual
  comparison is needed before deleting a newly added item.
- Treats a committed restore as successful if N.I.N.A.'s WPF layout observer
  throws after the item has already been inserted. This prevents a stale row
  and a duplicate from a second click.

## Package

- File: `SequencerItemRecovery.zip`
- SHA-256: pending final clean-checkout package
- Minimum N.I.N.A. version: `3.2.0.9001`

## Human-maintainer statement — add only after review

Jost Jahn has reviewed the source, dependencies, licence, provenance and
release artifact; performed and recorded the manual N.I.N.A. tests; and is the
accountable human maintainer. AI assistance was used for drafting and project
structure, including spelling, translation, formatting, source comments and
documentation.
