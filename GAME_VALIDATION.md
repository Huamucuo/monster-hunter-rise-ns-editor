> Public-preview packaging note (2026-10-12): This original R10 record is retained for provenance and feature/evidence history. Actual catalog content, historical local test reports and Git history are excluded from this unpublished snapshot. Device-dependent development and validation are paused. Current attachment hashes and fresh preparation checks are recorded in MANIFEST.sha256.json and PREPARATION_VALIDATION.md.

# Game validation workflow

This workflow prepares independent experiments using only the editor's existing fixed-capacity operations. It never operates the game. Each case starts from the same original four-file source, including a `00-baseline` control. Do not import backups, manifests or reports: the `import/` folder contains exactly four save files.

Build with `dotnet build Desktop\RiseWorkshop.csproj --configuration Release`. The desktop's **Game validation…** window prepares a selected plan and checks a returned bundle. It uses fresh selections, not the main window's pending edits. CLI usage:

```powershell
dotnet run --configuration Release -- validation-prepare "C:\example\source" 2 plan.json "C:\example\new-run"
dotnet run --configuration Release -- validation-verify-return "C:\example\new-run\01-money\manifest.json" "C:\example\report.json" "C:\example\returned" "C:\example\new-result.json"
```

Paths above are placeholders. The output root and result file must be new. All four recognized candidates are required. Plans require the selected file's exact SHA256 and may bind all four hashes; stale plans fail. IDs must be unique simple letters/digits/hyphen/underscore labels; `00-baseline` is reserved. ToolRevision identifies the implementation snapshot actually used.

Plan example (synthetic placeholders, not game values or legal limits):

```json
{
  "ExpectedSourceSha256": "REPLACE_WITH_SELECTED_FILE_SHA256",
  "ToolRevision": "REPLACE_WITH_SOURCE_REVISION",
  "Cases": [
    {"Id":"01-money","Kind":"money","Value":2001},
    {"Id":"02-points","Kind":"points","Value":5001},
    {"Id":"03-quantity","Kind":"item-quantity","RecordIndex":0,"Value":2},
    {"Id":"04-add","Kind":"add-known-item","ItemId":67108913,"Value":1},
    {"Id":"05-delete","Kind":"delete-item","RecordIndex":2},
    {"Id":"06-pair0","Kind":"talisman-pair","RecordIndex":3,"Position":0,"SkillId":2,"SkillLevel":3},
    {"Id":"07-pair1","Kind":"talisman-pair","RecordIndex":3,"Position":1,"SkillId":2,"SkillLevel":3}
  ]
}
```

Optional `ExpectedBundleHashes` is an object keyed by all four exact filenames. RecordIndex is the existing editor record index, starting at zero. Position is zero or one. The two talisman positions are distinct test families. No-op effects, arbitrary unobserved item IDs/pairs, invalid parameters and absent mappings fail. Choose small, intentional changes yourself; the service does not invent game limits.

Each case writes backups outside `import/`, an append-only manifest, instructions and a blank human report. The manifest contains immutable source/export hashes, source profile, selected slot, unknown game version, operation/record selectors, raw pre-game changed spans and before/expected semantic snapshots. Inventory expectations use quantity totals per ID, allowing native merging and reordering. Equipment comparisons use a full semantic multiset, including decoration IDs and talisman pairs. Character identity is a local fingerprint, not a displayed name in the manifest. Other slots are also witnessed. Unknown bytes may change after a game resave; unrelated domain differences are reported for investigation.

Fill the report only from your actual observations:

```json
{
  "CaseId":"01-money",
  "ExportSha256":"COPY_SELECTED_EXPORT_HASH_FROM_MANIFEST",
  "GameVersion":null,
  "Platform":null,
  "GameLoadSuccess":null,
  "EffectSuccess":null,
  "ActualValues":null,
  "ManualResavePerformed":null,
  "GameResaveSuccess":null,
  "Notes":null,
  "PhotoReferences":[]
}
```

`ActualValues` is your written observation of the intended before/after values; it is human evidence, not machine-parsed gameplay. Use true/false only after observing the event. Attempting a save is separate from a successful native save. Explicit load, effect or native save failure yields FAILED. Missing values, unknown version/platform and unanswered flags remain pending. Reports/photos are local and never uploaded.

The exact statuses are NOT_TESTED → EXPORTED → GAME_LOAD_OK → EFFECT_CONFIRMED → GAME_RESAVE_OK → EDITOR_RELOAD_OK, or FAILED. The editor cannot produce the three gameplay stages without affirmative human evidence. EDITOR_RELOAD_OK additionally needs a separately returned complete bundle whose selected file differs from the export and whose semantic witnesses match. Identical returned selected bytes remain pending: copying an export cannot demonstrate a fresh native save. A changed file plus a human report still does not mean the tool independently observed gameplay; that distinction is saved in every result.

Verification writes a NEW result with manifest/report hashes, returned hashes, evidence scope and comparisons. It does not modify the original manifest/report or earlier results. Keep per-run history. A successful chain recommends promotion only for the exact tested operation, input, game version and platform; it never automatically updates the feature matrix. The repository matrix starts NOT_TESTED and synthetic checks never promote it.

Public core API: `GameValidation.Prepare(sourceDirectory, slot, ValidationPlan, outputRoot)`, `GameValidation.VerifyReturn(manifestPath, reportPath, returnedDirectory, newResultPath)`, `Capture(DirectoryEditor)` and `Compare(expected, actual)`. `DirectoryEditor.ExportCopies(outputDirectory, backupParent)` can separate backups from import files.

Read-only presentation: `IPresentationNames.Label(namespace,id)` and `PresentationNames` provide numeric fallback. Optional caller-provided records require source/license metadata; namespaces Item, EquipmentType, Equipment and Skill cannot collide. Equipment keys additionally include kind, so equal numeric weapon/armor IDs remain separate. Display-only name/type columns leave numeric record targets unchanged. At the historical R6 stage no catalog was bundled or auto-loaded. Since R8 the attributed MHRice bundle is registered locally, but unverified names/categories appear only after explicit candidate opt-in; default presentation remains numeric. `DomainEditor.EquipmentSchema(index)` exposes observed field IDs/tags/width/count and read-only values. Auxiliary arrays remain UNKNOWN, not an editable socket-level list. No new write fields or dynamic writer were added.


## R9 manual checks

Start with a separate no-edit baseline and keep original files/backups untouched. Prepare one independent exported copy per behavior. Record platform/game version and screenshots of the original and resulting inventory/equipment.

1. Select an inventory record, choose Split / move / merge, select an empty or same-ID target, enter quantity and confirm. Test split, complete move and merge separately. Observe whether the game preserves the intended total and how it rearranges/merges records; compare semantic same-ID totals rather than relying only on row index.
2. Open Equipment draft on an existing weapon, armor or talisman. Clear one occupied reference or all three; apply and export. Observe equipped jewels, resulting skills and jewel stock separately. Clearing a reference is not a proven inventory refund. Test restoring only the original position in a separate case.
3. On a talisman, choose both original-observed skill/level pairs, optionally combine with clearing decorations, then Apply once. Check both effects, other equipment fields and jewel stock. Cancel must have no effect; Undo restores the whole applied action.
4. For each successful case, save natively in the game, record actual success, return a fresh copy, and reopen in the editor. Report failed loads/effects/save attempts honestly. Export/checksum/byte matches alone do not confirm gameplay acceptance.

Unknown socket capacities and auxiliary values are not editable. Do not treat original stack bounds or candidate catalog metadata as game maxima.
