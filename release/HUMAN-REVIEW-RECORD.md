# Human review record — Sequencer Item Recovery 0.1.11.0

Status: pending Jost Jahn's manual review. This record must not be marked
complete from automated tests alone.

## Automated evidence already available

- Build and unit tests: 13/13 passed on 2026-09-12.
- Installed and manually tested DLL SHA-256:
  `F9FB691DE68BEE904F452294D52035E90CA86A961E55862881CBBE6D7B3F326C`.
- Current source-build DLL SHA-256 after adding the public repository
  metadata:
  `63DEC8476CC4FBA684891A5274D8BADBA094B4115109124D0D755F5A3E717CAB`.
  The clean-checkout release artifact still requires its separate final
  review.
- The new regression test reproduces a N.I.N.A. UI observer exception after
  the sequence collection has already accepted the restored item. It proves
  that the committed insertion is now treated as successful instead of
  leaving a stale recovery row that can create a duplicate on a second click.
- The N.I.N.A. 3.2.0.9001 log records that version `0.1.11.0` loaded
  successfully on 2026-09-12.
- A release ZIP for this candidate has not been staged yet.
- N.I.N.A. loader evidence and manual checks must be recorded for 0.1.11.0;
  results from prior candidates do not sign off this change.

## Jost Jahn's required manual checks

- [x] Verified by Jost Jahn on 2026-09-12: the plugin appears as
  **Sequencer Item Recovery** under `Options → Plugins → Installed`, showing
  `Author: Jost Jahn` and version `0.1.11.0`.
- [x] Verified by Jost Jahn on 2026-09-12: its Imaging four-square icon opens
  the panel again after the panel is closed.
- [ ] After loading a saved test sequence, confirm that removing one harmless
  test instruction produces the expected row and that `'Restore'` returns it
  at the original position.
- [ ] In a new, unsaved test sequence, confirm that adding and then removing a
  harmless instruction without using `'Compare now'` produces the expected
  row and that `'Restore'` returns it.
- [x] Verified by Jost Jahn on 2026-09-12: the independently removed
  `'Wait For Time'` and `'Wait For Moon Altitude'` instructions produced two
  recovery rows. Each instruction was restored with one click, the other row
  remained usable, and no stale row or duplicate instruction remained. The
  N.I.N.A. log records two successful repetitions of this test.
- [x] Verified by Jost Jahn on 2026-09-12: removing the test block
  `'Parallel End of Sequence Instructions'` produced one recovery row.
  `'Restore'` returned the complete block and its contained instructions, and
  the recovery row disappeared. The N.I.N.A. log records successful
  completion without an exception.
- [x] Verified by Jost Jahn on 2026-09-12: during a harmless running sequence,
  the lower-right N.I.N.A. `'Pause'` control paused the sequence. The safety
  confirmation was accepted, `'Restore'` returned the deleted instruction,
  and N.I.N.A. then continued and completed normally. No cancel or abort
  workflow was used.
- [x] Verified by Jost Jahn on 2026-09-13: the source, licence, dependencies,
  tests, security and privacy behaviour, public README, NOTICE and the
  AI-assistance statement were personally reviewed. That statement records
  support with drafting, structure, spelling, translation, formatting,
  comments and documentation; it does not transfer responsibility from
  Jost Jahn.
- [ ] The final release package contents and SHA-256 value were personally
  reviewed after producing the immutable release candidate.

## Sign-off

- Reviewer: Jost Jahn
- Review date: ____________________
- N.I.N.A. version: 3.2.0.9001
- Result / notes: ____________________
