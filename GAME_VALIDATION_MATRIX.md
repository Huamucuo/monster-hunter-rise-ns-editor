> Public-preview packaging note (2026-10-12): This original R10 record is retained for provenance and feature/evidence history. Actual catalog content, historical local test reports and Git history are excluded from this unpublished snapshot. Device-dependent development and validation are paused. Current attachment hashes and fresh preparation checks are recorded in MANIFEST.sha256.json and PREPARATION_VALIDATION.md.

# Game validation matrix

Run R6-S2: seven independent cases from the user-selected original-backup slot 2. The reviewer exported and independently checked the complete four-file bundles. Game version is still unknown. No actual game load, effect observation, native resave or returned-save evidence has been received.

| Operation | Case | Status | Game load | Effect | Native resave | Editor reload of native resave |
|---|---|---|---|---|---|---|
| Money | 01-money | EXPORTED | Pending | Pending | Pending | Pending |
| Points | 02-points | EXPORTED | Pending | Pending | Pending | Pending |
| Existing item quantity | 03-quantity | EXPORTED | Pending | Pending | Pending | Pending |
| Add known item into empty record | 04-add | EXPORTED | Pending | Pending | Pending | Pending |
| Delete item | 05-delete | EXPORTED | Pending | Pending | Pending | Pending |
| Talisman skill slot 1 (API position 0) | 06-skill1 | EXPORTED | Pending | Pending | Pending | Pending |
| Talisman skill slot 2 (API position 1) | 07-skill2 | EXPORTED | Pending | Pending | Pending | Pending |

Control `00-baseline` is also EXPORTED and byte-identical to its source bundle. It must be tested first to distinguish existing-save/version issues from edits. All seven feature states changed NOT_TESTED -> EXPORTED in this phase. No row reached GAME_LOAD_OK or later.

Allowed states: NOT_TESTED / EXPORTED / GAME_LOAD_OK / EFFECT_CONFIRMED / GAME_RESAVE_OK / EDITOR_RELOAD_OK / FAILED. Explicit load/effect/save failure is FAILED. Absent reports remain pending. Re-reading the editor export is not native game resave validation. Test-generated fake manual reports do not contribute to this matrix.

A gameplay-write FEATURE_MATRIX promotion to VERIFIED requires the whole export -> actual game load -> correct observed effect -> successful native resave -> correct editor reread chain, with source hashes, game version, platform and exact operation scope. One accepted item/skill pair does not validate all IDs/rolls/game versions. No feature was promoted this phase.

Local run artifacts and Chinese steps contain user data and stay outside this repository's tracked source. Each local case manifest binds source/export hashes and raw changes. Review evidence is recorded in the parent workspace's R6 report; do not redistribute the save bundles as source attachments.

## R9 new behaviors (2026-10-05)

| Operation | Status | Game load | Effect | Native resave | Editor reload |
|---|---|---|---|---|---|
| Inventory split to empty | NOT_TESTED | Pending | Pending | Pending | Pending |
| Inventory full move to empty | NOT_TESTED | Pending | Pending | Pending | Pending |
| Inventory same-ID merge | NOT_TESTED | Pending | Pending | Pending | Pending |
| Weapon decoration clear / original-position restore | NOT_TESTED | Pending | Pending | Pending | Pending |
| Armor decoration clear / original-position restore | NOT_TESTED | Pending | Pending | Pending | Pending |
| Talisman decoration clear / original-position restore | NOT_TESTED | Pending | Pending | Pending | Pending |
| Talisman combined two-pair and decoration draft | NOT_TESTED | Pending | Pending | Pending | Pending |

R9 synthetic and reviewer byte checks are software evidence only; these new rows have no native-game report. Prior R6 EXPORTED evidence is preserved.
