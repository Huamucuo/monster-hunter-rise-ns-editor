# Approved source-publication boundary

This repository starts new history from the reviewed source-only snapshot of R10, **not a clone of the existing catalog-bearing repository**. The maintainer explicitly approved the new-public-history strategy, exact attachment allowlist and MIT grant on 2026-10-12. The original local history is preserved separately.

The original `clean/foundation` project at commit `f041cdc8408e9eadc88b05fb95d884e25a8d1198` has 16 branch-reachable commits. Actual `data/mhrice/catalog.json` is in the current tree and six of those commits. Removing it from the working tree or adding an ignore rule would leave the historical blobs reachable. **NORMAL PUSH BLOCKED** for that original history within the user's no-game-resource publication scope.

The source project's complete Git directory, all commits, refs, reflogs and local files are retained locally. No reset, delete, history filtering, squash, rebase or force-push was performed. Three additional commits were also observed in reflogs outside current branch reachability; they remain original local records and are not imported here.

The approved snapshot preserves a source commit reference and original source/provenance records so its origin is explicit. It does not manufacture earlier public commits or claim the original history was clean. The approved publication scope is source development only; it does not approve a stable release, device operation or excluded data.

Alternative: preserve the existing history for a later public route only after reviewing every historical attachment and establishing permission for content that the current publication scope excludes. This preparation does not grant that permission or execute that route.

Only files in the current `MANIFEST.sha256.json` are included. The manifest excludes its own hash to avoid circularity. `PUBLIC_FILES.md` is the human-readable list; `tools/check_public_snapshot.py` checks the snapshot boundary and hashes. Generated local build/test outputs are not public attachments. No Windows binary release is prepared.

The approved own-code MIT grant is in LICENSE; LICENSE-PROPOSAL.md records the exact decision and text. Public catalog reuse or game-content licensing is not being approved by attribution alone.

## R11 reviewed source synchronization

The maintainer explicitly authorized offline name editing, original-template equipment additions, whole-Build staging and reviewed GitHub synchronization on 2026-10-12. This supersedes the earlier software-development pause for that scope. Native-device testing remains unavailable while the NS is lent out; no stable release or binary distribution is implied.

R11 public additions consist of independently implemented source/tests, unchanged reviewed `docs/specs/r11` packets, an input identity record and concise documentation. Original-source type/ID template copies and measured owner text changes remain WRITE_EXPERIMENTAL; native R11 rows remain NOT_TESTED. No catalog, save/export/backup, player data, private report, screenshot, probe, archive, build cache or excluded original history is included. Root review independently checks private inputs and refreshes PUBLIC_FILES.md/MANIFEST.sha256.json before synchronizing the reviewed source; the isolated implementer does not push or publish.
