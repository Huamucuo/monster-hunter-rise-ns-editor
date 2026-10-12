# Implementation notes — review candidate, not release documentation

Author credit: 花木错. This foundation was written with GPT assistance from the six admitted specification files. No release license or originality certification is supplied.

## Scope and independent design choices

The new implementation has three boundaries: `Document` inspects immutable byte snapshots; `CopySession` handles explicit file selection and copy state; the command-line entry point formats status. A separate synthetic console runner exercises these boundaries. Files and classes were created from empty files for this work. No prior API compatibility is intended.

Only the observed version-2 value-0/value-8 envelope is recognized. Value 0 preserves an opaque remainder. Value 8 validates admitted length fields and uses the platform raw-DEFLATE decoder. A one-byte region reader prevents read-ahead from hiding unused compressed bytes and throws if the decoder requests bytes beyond the declared region. Decoded bytes are counted and discarded; no game records are parsed. Output is limited to exact original-byte copies. Integrity and game semantics always remain UNVERIFIED.

Input and decoded expansion are capped at 64 MiB. Unchanged serialization clones the original snapshot. All edit requests fail as UNSUPPORTED, including multi-field requests. Capability selection accepts only the explicitly admitted evidence ID and version, always with write capability false. No executable plugins are loaded.

File operations currently require Windows. Selection stores the source identity and SHA-256. Export reopens the source without following an endpoint reparse point, compares identity and digest, and holds the read handle while copying. Existing destinations, hard links, reparse ancestors, network/device namespace prefixes and alternate streams are rejected. The immediate parent directory is pinned without delete sharing; ancestors are checked before and after pinning.

Backup files are created in a unique directory with protected current-user-only inherited access, then output is created with CreateNew. Both copies are flushed and re-read for digest comparison before success. Restore checks the supplied expected digest and creates a new file. It never overwrites the selected input or the selected backup. Expected restore digests must come from the user's trusted backup record; this is not cryptographic proof of authorship.

Failure policy: a created partial file is deleted after failure. If deletion also fails, the error reports both failures and the leftover destination; no success is returned. A fully verified backup remains available when subsequent output creation fails. Empty backup directories may remain. Unknown source bytes and trailing bytes outside the compressed region are preserved exactly.

## Safety boundary and limitations

The implementation rejects detected links and ordinary aliases, uses no-follow file handles, pins immediate parents and denies source write/delete sharing during export. It does not claim resistance to a hostile process running with the same user or administrator privileges, concurrent replacement of higher ancestor directories, filesystem/driver failure or process termination during output creation. A killed process may leave an incomplete destination; no success receipt would have been emitted. This is not an atomic crash-recovery transaction.

An attempted stronger all-ancestor directory-lock implementation failed in this environment with Windows error 5 at an ancestor above the repository. The final implementation explicitly bounds locking to the immediate parent and does not claim OS-enforced protection for the entire path ancestry. This limitation remains a review item for any stronger security requirement.

Single-byte decoder reads prioritize strict region accounting over throughput; very large files may decode slowly. The synthetic suite supports the tested .NET 10 runtime behavior, not every DEFLATE implementation or every possible malformed stream. No original saves were available to this implementer, and no game was run. Synthetic bytes are not game-generated evidence. Real-sample inspection, game editing, bilingual graphical UI, decoration/augmentation mappings and release licensing remain outside this foundation.

## Actual validation

SDK: 10.0.102. Final synthetic execution: 44 cases, 0 failures, recorded in `test-results.json` with UTC time and baseline hashes before/after. Cases include every shorter prefix of a synthetic compressed region, an extra byte within that region, mismatched decoded lengths, malformed DEFLATE, oversized declarations and inputs, immutable snapshots, exact round trips, edit rejection, explicit selection states, backup/restore identity, source replacement/change, hard links, junctions, existing destinations, digest mismatch, partial-file cleanup and CLI statuses.

Earlier runs were not passing: the first run had 1/43 failures because the test generator emitted no DEFLATE stream for an empty payload; its fixture was corrected to the valid empty raw stream 03 00. A later stronger-lock run had 6/44 failures due to the environmental ancestor handle restriction. These failures are retained here rather than labeled passing. The final passing result tests the documented immediate-parent lock scope.

Reproduction commands from this repository:

```
dotnet build --configuration Release
dotnet run --configuration Release --no-build -- --self-test
```

CLI forms (all file choices explicit):

```
dotnet run --configuration Release -- inspect <file>
dotnet run --configuration Release -- export <file> <new-output> <backup-parent>
dotnet run --configuration Release -- restore <selected-file> <backup> <expected-sha256> <new-output>
```

Recognition establishes only the outer observation profile. GameIdentity is also UNVERIFIED; arbitrary matching bytes are not established as a Monster Hunter Rise Switch save.

MHRice name-provider phase adds IGameCatalogProvider, LocalGameCatalogProvider, MhriceCatalogProvider and CatalogProviderNames. Trusted NS mapping construction is unavailable until evidence is admitted; JSON claims and numeric overlaps cannot promote mappings. Standard Release packages the separately attributed data and notices. Five provider/resource groups and bilingual synthetic-save UI smoke pass. Full editor readiness still has functional gaps (equipment writes, dynamic arrays, character-name writes, verified ranges/mappings and gameplay evidence); narrower experimental publication has separate provenance/rights/package gates. No .msg reader implemented.

R9 public domain APIs: OriginalStackCeiling(id), NextEmptyInventoryRecord(), TransferItem(source,destination,quantity), ReadEquipmentDraft(index), ApplyEquipmentDraft(opened,decorations,pairs). The latter accepts null to leave an optional section unchanged, checks source identity and stale selected fields, builds all primitive replacements before a single Apply, and skips no-op history. The directory wrapper ApplyDomain preserves grouped Undo. No new disk-writing path was added. UI draft changes remain detached until Apply; Cancel does not refresh/stage. Existing single-pair API stays compatible, while normal GUI uses the combined draft.

R9 follow-up: the retained WorkspaceChecks inputs predated the conservative policy. Prior cases requested currency/item expansion that production correctly rejected. Updated only tests to use money/points decreases and freed same-ID quantity reallocation, added explicit expansion-rejection assertions, and initialized edit/export/undo cases independently. Mirror Undo/Clear, complete bundle, unchanged other slots, exact payload comparison and folder context checks remain. --self-test-r5 now records test-results-r9-workspace-regression.json (12/12 passing), preserving historical test-results-r5.json. No production ranges were relaxed.

R9 scoped review correction: equipment details wrap to their actual panel width with content-sized footer height; no-result filtering clears stale detail text. Embedded new draft/decoration explanations translate in both language directions. Category relabeling retains its underlying selected category/record and staged work. Describe keeps ID/source/license/status visible but suppresses candidate names until opt-in. GAME_VALIDATION.md now distinguishes the historical R6 absence of catalogs from the current R8 opt-in bundle. No domain write policy changed.

R10 independent implementation: added nullable catalog metadata records, BuildPlanner with immutable outward piece clones, explicit part mapping (Head/Chest/Arm/Waist/Leg), gated reference/related-skill search, partial totals and versioned numeric-only local plan persistence. BuildForm/BuildPieceForm provide detached offline choices; no save-domain methods are used by the planner. Catalog base slots are never assigned as proven current equipment capacities. Actual-save and virtual-talisman values remain distinct.

History extension keeps inverse primitive batches in EditorWorkspace and groups their depth boundaries in DirectoryEditor. Successful changes discard redo; no-op Apply skips history. ReviewChanges returns copied original/current bytes and known capability labels where admitted, with unknowns explicitly opaque. No new write field or export path. Existing R9 and conservative workspace checks passed again with R10-specific retained reports; earlier reports remain historical.

R10 consolidated review corrections: actual EN/ZH numeric-control tests first reproduced the nonempty-skill/zero-level exception, and real catalog callback/Build-factory tests reproduced local selection loss. Numeric transitions now guard dependent events and use positive minima for nonempty skills; None keeps zero. Main window retains only successfully loaded catalogs and reuses the selection in Build. Shared per-piece presentation distinguishes virtual plan fields, catalog reference values and that piece's total including valid decorations. Permanent offline-slot labels distinguish absent/unknown capacity. Choice rebuilds retain the visible decoration query and capacity restriction. Minimum English introduction wraps to actual parent width. These changes do not touch save writing.
