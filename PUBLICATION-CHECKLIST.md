# Sequencer Item Recovery — publication checklist

Status: public source repository authorised and being created. No GitHub
Release or N.I.N.A. manifest pull request has been created.

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
- Local manifest draft: `release/manifest.template.json`.
- Human review record: `release/HUMAN-REVIEW-RECORD.md`.

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
