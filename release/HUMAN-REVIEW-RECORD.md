# Human review record — Sequencer Item Recovery 0.1.11.0

Status: completed by Jost Jahn. Automated tests were supplemented by the
manual N.I.N.A. checks recorded below.

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
- The immutable local release candidate was staged from clean public commit
  `efdb784`; its ZIP SHA-256 is recorded below.
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
- [x] Verified by Jost Jahn on 2026-09-13: the final release package contains
  only `SequencerItemRecovery.dll`. The approved ZIP SHA-256 is
  `923F9AFDE3D2CC5F19CD981088EA3A0B6FF395AFE093901FDE5E541923F75A64`.
- [x] Verified by Jost Jahn on 2026-09-13: the DLL extracted from the approved
  ZIP was installed with N.I.N.A. closed and its SHA-256 was verified as
  `27347E3D7A455C7AF983BE7CC64674668F0A6ADD800857023CCC94643626733C`,
  N.I.N.A. loaded it successfully, and a final removal and `'Restore'` smoke
  test restored the `'Dusk'` instruction with this exact artifact.

## Sign-off

- Reviewer: Jost Jahn
- Review date: 2026-09-13
- N.I.N.A. version: 3.2.0.9001
- Result / notes: Passed. Technical and human review is complete; publication
  still requires Jost Jahn's separate explicit approval.
