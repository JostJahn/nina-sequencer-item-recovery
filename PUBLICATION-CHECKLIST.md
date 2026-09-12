# Sequencer Item Recovery — publication checklist

Status: public source repository and GitHub Release `v0.1.11.0` published.
The N.I.N.A. manifest was submitted in pull request `#690` and is awaiting
the repository maintainers' workflow approval and review.

Local Git repository: branch `main`. Public remote:
`https://github.com/JostJahn/nina-sequencer-item-recovery`.

## Accountable maintainer

- Responsible human maintainer: **Jost Jahn** (confirmed on 2026-09-10).
- Public author/publisher: **Jost Jahn** (confirmed on 2026-09-10).
- Before a public release, the maintainer must personally review the source,
  dependencies, security/privacy implications, licence/provenance, and the
  released artifact.

## Current plugin facts

- Public plugin name: `Sequencer Item Recovery`
- Assembly identifier: `3c52a960-96a7-41a6-a06a-4aa54f4c0df9`
- Current version: `0.1.11.0`
- Minimum N.I.N.A. version: `3.2.0.9001`
- Current build output: `src/NinaSequenceDiff/bin/Release/net8.0-windows/SequencerItemRecovery.dll`
- Licence: `MPL-2.0`, confirmed by Jost Jahn on 2026-09-12. The complete
  licence text is in `LICENSE`.
- Chosen public repository: `JostJahn/nina-sequencer-item-recovery`.
- Submitted manifest source: `release/manifest.template.json`.
- Human review record: `release/HUMAN-REVIEW-RECORD.md`.
- Published release:
  `https://github.com/JostJahn/nina-sequencer-item-recovery/releases/tag/v0.1.11.0`.
- Published ZIP SHA-256:
  `923F9AFDE3D2CC5F19CD981088EA3A0B6FF395AFE093901FDE5E541923F75A64`.

## Maintainer decisions

- [x] Jost Jahn approved the public README, attribution and AI-assistance
  disclosure on 2026-09-12.
- [x] Jost Jahn approved the MPL-2.0 licence text and NOTICE on 2026-09-12.
- [x] Jost Jahn reviewed the source, dependencies, tests, security and privacy
  behaviour on 2026-09-13.

## Release and manifest sequence

1. Create a public source repository and push the reviewed source only after
   maintainer approval.
2. Build and test the release configuration from a clean checkout.
3. Create an immutable version tag and GitHub Release containing the final DLL
   or ZIP. Do not rebuild or replace that artifact after calculating its
   checksum.
4. Calculate the release artifact's SHA-256 checksum.
5. Generate a N.I.N.A. manifest from that release artifact. Its installer URL
   must be a direct, stable download URL; its identifier must match the
   assembly GUID.
6. Validate the manifest with `node gather.js` in the official manifest
   repository, then submit it in a pull request.
7. Record the N.I.N.A. test version and visible manual smoke test in the
   release notes.

## Current progress

- [x] Public source repository created and reviewed source pushed.
- [x] Release configuration restored, tested and built from clean public
  commit `efdb784`.
- [x] Immutable local ZIP created, its contents and SHA-256 reviewed, and its
  exact DLL smoke-tested in N.I.N.A. 3.2.0.9001 by Jost Jahn.
- [x] Immutable tag `v0.1.11.0` and approved GitHub Release published. The
  publicly downloaded ZIP was verified byte for byte against the approved
  artifact on 2026-09-13.
- [x] Final N.I.N.A. manifest generated and validated with both official
  validation scripts.
- [x] Manifest submitted to the official repository in pull request
  `https://github.com/isbeorn/nina.plugin.manifests/pull/690`.

## Required human release check

Jost Jahn performs and records this check before creating the public release:

1. Review the source, licence text, package contents and SHA-256 value.
2. In a separate test profile, install the final ZIP with N.I.N.A. closed and
   start N.I.N.A.
3. Open an Advanced Sequencer sequence, remove a test instruction and use
   Sequencer Item Recovery's `'Restore'` action to return it to the original
   position.
4. Start a harmless test sequence, use N.I.N.A.'s lower-right `'Pause'`
   control, restore a test deletion, confirm the pause dialog, inspect the
   sequence and resume it. Do not use cancellation or abort as part of this
   test.
5. Restart N.I.N.A. and confirm that the plugin loads, the Imaging icon opens
   Sequencer Item Recovery, and no unexpected errors appear in the log.
6. Record the N.I.N.A. version, test date, reviewer name and result in the
   release notes. The accountable reviewer is Jost Jahn.

## Draft AI-assistance disclosure — requires maintainer review

> AI assistance was used while drafting and structuring the project, including
> spelling, translation, formatting, source comments and documentation. Jost
> Jahn reviewed the source before source publication and will review the final
> release artifact before release publication. He acts as the accountable
> human maintainer.

Use that wording only after the listed review has actually been completed.

## Official references

- N.I.N.A. central manifest process: <https://github.com/isbeorn/nina.plugin.manifests>
- N.I.N.A. plugin template and assembly metadata: <https://github.com/isbeorn/nina.plugin.template>
