> R11 update (2026-10-12): Offline application development resumed by explicit maintainer request. The published source preview excludes actual catalogs, private reports, saves and old Git history. Native-device testing remains unavailable while the NS is on loan. The current table and R11 section describe the new software; dated R5–R10 sections retain historical scope and results. Current attachment hashes are recorded by the final reviewed MANIFEST.sha256.json.

# Feature matrix

Status vocabulary: NOT_STARTED / RESEARCHING / READ_ONLY / WRITE_EXPERIMENTAL / IMPLEMENTED / VERIFIED.
For gameplay writes, VERIFIED requires the full scoped export/load/observed-effect/successful-native-resave/editor-reread chain. Infrastructure VERIFIED means scoped software/byte-level verification on stated inputs and does not imply game acceptance. This matrix describes only the new implementation.

| Feature | Status | Current capability / remaining work |
| --- | --- | --- |
| Save identification | READ_ONLY | Observed DSSS v2/checksum/structure profile; no authenticated game-version mapping |
| Save reading | READ_ONLY | Full observed structural traversal and immutable source retention |
| Decode / encode | WRITE_EXPERIMENTAL | Raw-DEFLATE, fixed-width primitive replacement and explicit tag-15 text resizing with structural alignment reconstruction; no arbitrary schema/array expansion |
| Character slots | IMPLEMENTED | Explicit folder/file selection; independent status for three known slot filenames and system file |
| Character information | READ_ONLY | Name and independent primary rank paths; absent MR remains unknown |
| Character-name writing | WRITE_EXPERIMENTAL | Exact two local owner routes plus validated corresponding system name; one history group; conservative 1–16 UTF-16-unit application policy, not a verified game rule |
| Currency | WRITE_EXPERIMENTAL | Conservative 0..original money/points; folder money preview coupling with grouped Undo/Clear; not game maxima |
| Item reading | READ_ONLY | Numeric IDs/quantities with filtering and preserved record order |
| Item quantity editing | WRITE_EXPERIMENTAL | Same-ID staged total cannot exceed original positive total; zero normalizes an existing supported record |
| Add item | WRITE_EXPERIMENTAL | Redistribute freed original same-ID quantity into an existing empty record; no total expansion |
| Delete item | WRITE_EXPERIMENTAL | Normalize selected record to ID 0x04000000/count 0; no resizing |
| Weapon reading | READ_ONLY | Numeric type/ID and read-only decoration IDs |
| Weapon editing | WRITE_EXPERIMENTAL | Decoration-reference clearing/original-position restoration only; base attributes, upgrades and augmentation unsupported |
| Armor | WRITE_EXPERIMENTAL | Decoration-reference clearing/original-position restoration only; base armor attributes unsupported |
| Talismans | WRITE_EXPERIMENTAL | Existing type-3 talismans; atomic two-pair plus decoration draft using original observed pairs |
| Skills / affixes | WRITE_EXPERIMENTAL | Only the two reviewed talisman pair positions; no arbitrary skills/levels, affixes or names |
| Rarity / quality | NOT_STARTED | No admitted editable domain mapping |
| Add equipment | WRITE_EXPERIMENTAL | Original unchanged type/source-ID template copies into existing type0/ID0 records with identical complete recursive metadata; quantity preview, default decoration clear, explicit source-decoration opt-in |
| Add Build to equipment box | WRITE_EXPERIMENTAL | All seven owned-template positions or six/None work without catalog; each non-None piece resolves or all reject; one Apply/Undo/Redo; no auto-equip |
| Variable-length structure | WRITE_EXPERIMENTAL | Tag-15 text only; original logical identities/counts preserved, compatible common nonzero padding retained and new padding zeroed |
| Checksum | VERIFIED | Retained R2 vectors and separately reviewed source-content validation |
| Save modified copy | WRITE_EXPERIMENTAL | Desktop and service export; complete recognized four-file folder workflow, explicit partial-result reporting |
| Backup | VERIFIED | Retained R4 service validation; desktop receipt associated with selected-slot export |
| Restore | VERIFIED | Retained R4 service validation; desktop restores selected export's original backup into new path |
| GUI | IMPLEMENTED | English WinForms window, named domain tabs, structural browser; synthetic minimum/large-window smoke |
| Search / filter | IMPLEMENTED | Paged opaque-ID/class-ID/local-text search; numeric inventory/equipment filters, empty equipment hidden by default |
| Game validation workflow | IMPLEMENTED | Independent case exports, manual evidence gates and returned-bundle comparison; actual cases remain EXPORTED |
| Name catalog interface | IMPLEMENTED | Numeric fallback plus optional separately supplied local multilingual catalogs; kind/ID keys, search/filter/sort and declaration-only metadata; all candidates UNVERIFIED_MAPPING, no actual catalog in the public source |
| Equipment field schema | READ_ONLY | Reviewed numeric identity, decoration and talisman pair fields; unknown auxiliary values not editable |
| Version recognition | READ_ONLY | Envelope v2 only; no game-version inference from auxiliary array length |

## R5 transitions and evidence

- Character slots: NOT_STARTED -> IMPLEMENTED.
- Character information: RESEARCHING -> READ_ONLY.
- Currency: RESEARCHING -> WRITE_EXPERIMENTAL.
- Item reading: RESEARCHING -> READ_ONLY.
- Item quantity/add/delete: NOT_STARTED -> WRITE_EXPERIMENTAL, limited to fixed-capacity records.
- Weapon reading and Armor: RESEARCHING -> READ_ONLY.
- Talismans: RESEARCHING -> WRITE_EXPERIMENTAL; Skills / affixes: NOT_STARTED -> WRITE_EXPERIMENTAL only for reviewed talisman pairs.
- GUI and Search / filter: NOT_STARTED -> IMPLEMENTED.
- Save modified copy remains WRITE_EXPERIMENTAL but now has named GUI staging, linked directory export and reload.

Focused synthetic model checks: 12/12 passing in `test-results-r5.json`. This includes unrelated `_Data1` roots, complete-path ambiguity locality, independent/missing rank fields, money/points, item add/delete/quantity, talisman pair round-trip, grouped slot/system Undo/Clear, complete bundles, context switches and a 500k-event browser check. Latest measured 500k open/page/search timings are in that report, not universal performance guarantees.

Synthetic desktop smoke: eight screenshots at 1120×760 and 1500×940; current-cell selection and dropdown retention verified; zero clipped action buttons in the final layout report. No real save was accessed by the isolated implementer. The root reviewer owns separate actual-input regression and byte-oracle results. No status here claims game-tested legality or complete-editor readiness.

## R6 validation rule and supporting capabilities

Gameplay writes may reach VERIFIED only after export → actual game load → observed intended effect → successful native resave → editor reread, scoped to the tested version/platform/input and exact operation. Existing checksum/backup/restore VERIFIED entries describe infrastructure, not game acceptance. Human game observations are labeled human evidence. No synthetic tests or exported cases automatically promote any gameplay row.

Validation case preparation, immutable manifests and returned-bundle comparison: IMPLEMENTED (synthetic workflow checks). Presentation-name interface and read-only equipment schema: IMPLEMENTED with numeric fallback; no catalog bundled. The seven gameplay rows remain WRITE_EXPERIMENTAL pending actual game evidence. After stable evidence, equipment work has priority; dynamic structures remain lower priority and fixed capacity is retained where adequate.



## Interface language (2026-09-29)

English / Simplified Chinese interface switching: IMPLEMENTED. Visible top-level choice, English default and per-user persistence. Main/domain/validation controls and ordinary explanations switch in place; numeric IDs, staged changes and selected records remain unchanged. Synthetic bilingual smoke and preference tests passed. This UI capability does not alter gameplay validation statuses.

## Desktop layout refinement (2026-09-29)

UI layout: IMPLEMENTED. Grouped responsive toolbar, joined language selector, one tab layer, empty-state opening guide, separate character/currency cards and consistent grids. Verified by synthetic bilingual empty/loaded screenshots at 1120×760 and 1500×940 on the available 144-DPI (150%) host. Native 125% scaling was not tested. Existing staging/selection/language checks pass; gameplay statuses unchanged.


## Catalog and conservative policy (2026-09-29)

Optional catalog infrastructure: IMPLEMENTED; actual NS game-name mapping: RESEARCHING (external real-name candidates available, no verified NS mapping). Ordinary raw structure UI: READ_ONLY. Source-based conservative edit ranges: IMPLEMENTED, not verified game limits. Decoration slot filtering: presentation-only interface; actual slot writing remains NOT_STARTED. Historical increased-value exports remain historical EXPORTED evidence; their manifests and statuses were not rewritten.


Character-name editing: RESEARCHING (new explicit row); character-name reading stays READ_ONLY. UTF-16 storage measurement does not establish game limits or required synchronization, so no name writer is enabled.


## External name candidates (2026-09-29)

Provider architecture and bilingual opt-in/search: IMPLEMENTED. MHRice 7675 entries load from attributed bundled resources; candidate display defaults off each launch. All entries remain UNVERIFIED_MAPPING. No VERIFIED_NS_MAPPING, new write range, game-validation promotion or .msg extraction is implemented. Five provider/resource check groups pass; bilingual synthetic-save UI smoke checks candidate gating, name search and staging retention.


## R9 existing-record operations (2026-10-05)

| Feature | Status | Scope |
|---|---|---|
| Inventory split / move / merge | WRITE_EXPERIMENTAL | Atomic same-ID transfer between fixed records; preserved total; each resulting positive stack bounded by largest original same-ID stack, not game maximum |
| Equipment decoration references | WRITE_EXPERIMENTAL | Existing weapon/armor/talisman: three positions, each zero or original same-position ID; no stock refund or socket-capacity guarantee |
| Equipment draft | IMPLEMENTED | Cancel without staging; one Apply/Undo for decoration and both coupled talisman pairs; missing sections isolated |
| Candidate search/category | IMPLEMENTED | All available name languages plus decimal/hex ID; provider categories/names only under opt-in |

No native-game evidence added. Earlier EXPORTED cases remain historical. Equipment identities, auxiliary arrays, rarity, levels, augmentation, creation and dynamic structures are unchanged/unsupported. Full requested editor readiness remains incomplete; publishing a narrower experimental project has separate rights/provenance/package gates.

## R10 catalog references, offline Build and history (2026-10-05)

| Feature | Status | Scope |
|---|---|---|
| Equipment reference attributes | READ_ONLY | Opt-in UNVERIFIED catalog rarity, weapon attack/affinity/bonus/elements, armor defense/resistances/skills and catalog base slots; not actual augmentation capacities |
| Decoration skill search | IMPLEMENTED | Candidate-gated multilingual name/related-skill/numeric search and offline requirement 1..4 filter; no jewel write expansion |
| Seven-position Build planner | IMPLEMENTED | Independent local workspace; weapon/head/chest/arms/waist/legs/virtual talisman, detached piece confirmation/cancel, optional None, conflicts excluded visibly |
| Reference plan totals | IMPLEMENTED | Armor base defense plus weapon defense bonus, five signed resistances, independent weapon attack/affinity, skill totals/declared maximum/excess; missing data stays partial |
| Portable plan JSON | IMPLEMENTED | Versioned numeric selections/title only, explicit new-file Save/Open, unresolved IDs preserved, validation before replacement |
| Redo / staged-change review | IMPLEMENTED | Model-owned grouped Undo/Redo including linked system preview; branch replacement, Clear history, read-only original/current differences |

Planning never stages save changes and cannot create equipment in a save. No current-equipped mapping, new save field, range expansion, augmentation, rarity/level writer or gameplay verification was added. Full editor functionality and native-game evidence remain incomplete; narrower source publication still has independent rights/provenance/package gates.

## Current outstanding core functions after R11

| Requested function | Status | Remaining scope |
|---|---|---|
| Current-equipped references / switching | RESEARCHING | No admitted current loadout reference mapping; offline positions are not worn equipment |
| Fresh equipment identity creation | NOT_STARTED | No arbitrary identity/rarity/upgrade/instance initialization grammar; original-template copies do not establish one |
| Game name limits / complete mirror validity | RESEARCHING | Experimental measured mirrors are implemented; all required game mirrors and game-specific input rules remain unconfirmed |
| Virtual talisman socket output | NOT_STARTED | Virtual levels stay planning-only; opaque auxiliary arrays are not established as socket encodings |
| Augmentation / equipment base attributes | NOT_STARTED | External reference values are not editable save mappings |
| Appearance editing | NOT_STARTED | No admitted editable appearance fields |
| Buddy editing | NOT_STARTED | No admitted editable buddy fields |

R10 correction: transient virtual-skill controls remain valid, explicitly loaded catalogs persist into Build, slot labels always identify offline position/capacity, per-piece contributions include valid decorations, confirmed virtual talismans show their own pairs/slots, and search remains active across piece/slot changes. Gameplay states unchanged.

## R11 names, original templates and whole Build (2026-10-12)

| Feature | Status | Scope |
|---|---|---|
| Name draft and linked rename | WRITE_EXPERIMENTAL | Apply/reset, exact UTF-16 input, both required local owner routes, validated system mirror, local-only warning for explicit single-file use, atomic failed/no-op handling |
| Text resizing serializer | WRITE_EXPERIMENTAL | Growing/shrinking text across alignment boundaries, mixed primitive edits, strict expected original bytes, logical reinspection and untargeted-value preservation |
| Original equipment templates | WRITE_EXPERIMENTAL | All occupied original weapon/armor/talisman types, current staged settings, exact full recursive class/field/array prefix/count/mode/leaf checks, original source identity and stale-preview rejection |
| Whole Build to box | WRITE_EXPERIMENTAL | Explicit source configuration per selected piece, every non-None piece included, no partial staging on unresolved identity/jewel/virtual/capacity failures, no auto-equipping |
| Portable template plan | IMPLEMENTED | Schema 2 numeric selections with opaque digest hints; schema 1 remains readable; loaded bindings require current-source revalidation or explicit replacement |
| Bilingual confirmation UI | IMPLEMENTED | Character Apply/reset, quantity/source-decoration template preview, all seven owned-template choices including actual talisman/None, fixed actions/warnings and separately scrolling details |
| Copied talisman display | READ_ONLY | Current copied pairs can be displayed before export/reload; existing-equipment writers still require original occupied identities |

Reviewed inputs are the unchanged `docs/specs/r11` packets. Fresh focused synthetic checks and actual-control smoke commands are recorded in IMPLEMENTATION_NOTES_R11.md; reports/fixtures/screenshots remain outside public attachments. No synthetic or reviewer byte result promotes a gameplay row to VERIFIED. Native R11 rows remain NOT_TESTED in GAME_VALIDATION_MATRIX.md.
