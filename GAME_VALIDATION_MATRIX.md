> R11 update (2026-10-12): Offline software work resumed by explicit request; native-device testing remains unavailable while the NS is on loan. The R6/R9 records below retain their original evidence states. Fresh software tests, UI smoke and byte checks do not represent native game load/effect/resave acceptance.

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

## R11 new experimental writes (2026-10-12)

| Operation | Status | Game load | Effect | Native resave | Editor reload of native resave |
|---|---|---|---|---|---|
| Name rename with two local mirrors and linked system preview | NOT_TESTED | Pending | Pending | Pending | Pending |
| Original weapon template copies to empty box records | NOT_TESTED | Pending | Pending | Pending | Pending |
| Original armor template copies to empty box records | NOT_TESTED | Pending | Pending | Pending | Pending |
| Original talisman template copies with copied pairs/opaque fields | NOT_TESTED | Pending | Pending | Pending | Pending |
| Whole seven-piece template Build to box | NOT_TESTED | Pending | Pending | Pending | Pending |
| Six-piece Build with None talisman | NOT_TESTED | Pending | Pending | Pending | Pending |

No native acceptance evidence has been received for these rows. Software export/reload, independent byte comparison and visible controls do not move them to EXPORTED or GAME_LOAD_OK in the native matrix.

The current private regression exports are engineering samples for source/output comparison, not a formal device-acceptance bundle delivered to the user. They remain private and do not change the NOT_TESTED native states.

### Manual acceptance after a separately authorized device session

1. Identify the exact platform/game version and test a separate untouched baseline bundle first. Retain its hashes and all originals; do not attach the bundle publicly.
2. Rename a selected character through the draft and Apply action. Use separate long, short, CJK and non-BMP cases within the documented application policy. In directory mode, export the linked slot/system copies; check the selected owner name and slot preview, other characters/cards, native save success and the re-read native bundle. Explicit single-file use requires its own narrower mirror-limitation case.
3. For weapon, armor and talisman separately, choose an original owned source, add one copy into a previewed empty record, and export a new copy. Check box count, copied source configuration, default cleared decoration references, opaque-instance effects and unchanged original source record. Test the explicit decoration-copy option separately. Verify intended effect, successful native resave and editor re-read; record any rejected or duplicated-instance behavior as failure.
4. Choose explicit original templates for all seven Build positions, preview every source/destination, and add the set as one action. In the game verify every copy in the equipment box and that current worn gear was not switched. Repeat a separate six-piece case with None talisman. Complete native resave and editor reload for the complete set; one accepted piece does not validate the remaining pieces.
5. Record observations, exact source/implementation hashes and per-operation success/failure without promoting other IDs, configurations, mirrors or versions. Unconfirmed answers stay pending. Virtual socket output, fresh unowned identities and augmentation remain unsupported rather than device-test candidates.
