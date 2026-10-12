# Revision 4 implementation notes — experimental modified-copy service

Author credit: 花木错. GPT-assisted engineering from the reviewed R4 packet and prior admitted byte relationships. No new game-format or semantic fact is asserted. This is a separate experimental service, not a normal game editor or unrestricted switch. Earlier implementation notes and result files remain historical snapshots.

## Public API for independent review

```csharp
using FreshFoundation;

var session = new ExperimentalExportSession();
session.Select(explicitSourcePath);
Document selected = session.Selected!;
int eventIndex = Enumerable.Range(0, selected.Structure.Events.Count)
    .First(i => selected.Structure.Events[i].DataCoordinate is not null);
var occurrence = selected.Structure.Events[eventIndex];
byte[] oldBytes = selected.Structure.CopyPayload()
    .AsSpan(occurrence.DataCoordinate!.Value - 12,
            checked((int)occurrence.Width!.Value)).ToArray();
byte[] newBytes = (byte[])oldBytes.Clone();
newBytes[0] ^= 1; // Research byte operation only; no game semantics.
var requests = new[] { new PrimitiveReplacement(eventIndex, oldBytes, newBytes) };

ExperimentalExportReceipt receipt = session.Export(
    requests, explicitlyChosenNewOutputPath, explicitlyChosenBackupParent);
string restoredPath = session.RestoreOriginal(
    receipt.Backup, explicitlyChosenNewRestorePath);
```

Handle inputs with no primitive occurrences instead of assuming `First` succeeds. The request contract remains R3: stable event identity within this selected immutable document, exact expected original bytes and same-width replacement. Empty requests or identical replacements are supported no-ops, but still create a verified original backup before an exact output copy.

`ExperimentalExportReceipt` exposes `OriginalDigest`, `ModifiedDigest`, `Backup`, `BackupPath`, `OutputPath`, `Status=EXPERIMENTAL` and `GameAcceptance=UNVERIFIED`. The backup token is a `CompletedOriginalBackup` with read-only `Path`, `OriginalDigest` and `CreationVerification=VERIFIED_AT_CREATION`. Its constructor is internal; callers cannot supply arbitrary external result bytes to export or construct a backup token through the public API.

Catch `ExperimentalExportException` for export/restore failure. Inspect `FailureStage`, `CompletedBackup`, `IncompletePath`, `BackupValidation` and `InnerException`. `CompletedBackup` is null until original backup creation, flush and digest readback completed. On a later output failure, it identifies the already completed backup and can be passed to `RestoreOriginal`. If cleanup fails, `IncompletePath` names the newly created incomplete file and the nested aggregate retains both the primary and cleanup exceptions. Do not treat this as success.

A backup token proves verification at creation time, not current integrity. `BackupValidation` is NotChecked, Matched or Mismatch for the current operation. A restore digest mismatch reports current validation failure, preserves diagnostic token context and creates no output. It never says the now-damaged file is a valid original backup. Restore accepts an explicitly chosen completed-backup token and verifies its recorded digest before creating a new file; it never copies the changed export and calls that an original restore.

`State` distinguishes selection, preparation, backup work/completion, output work/completion, restoration and failure. `Selected`/`SourcePath` are cleared before an ordinary new selection attempt and remain cleared if it fails. A rejected reentrant operation is not a new selection attempt. Session operations are serialized, with an explicit running guard in addition to locking: user-defined request Count/indexer code cannot reenter selection or switch the source while a transaction is bound to it.

## Transaction and filesystem behavior

All requests are validated and serialized in memory before side effects. The service then holds the selected file against ordinary write/delete sharing, checks its original file identity and full SHA-256, and creates a new current-user-private backup directory. The first written file is an exact copy of the original input, not the modified result. Both backup and output use exclusive CreateNew, flush and digest readback. Success evidence is returned only after output verification.

Existing source, output and backup files are never overwritten. Source paths, directories, ADS/network/device paths, hard-linked sources and detected reparse ancestors are rejected. Reserved Windows DOS device components such as NUL and CON were additionally rejected in the shared normalizer. No platform expansion occurred; this remains a local Windows service. The existing immediate-parent pinning and ancestor checks remain, with no claim of hostile concurrent higher-ancestor substitution resistance.

The existing shared copy primitive was extended with internal-only timing/fault probes and split writes. Production entry points have no public fault/probe options. The same actual exclusive creation, write, flush, verification and cleanup workflow is exercised by injected tests; this is not a separate mock exporter. Ordinary caught failure removes only a file newly created by the attempt. Pre-existing files are never marked owned or removed. A completed original backup survives subsequent output failure. Empty newly created backup directories may remain after failure.

A process crash or power loss may leave an incomplete file because this is not a crash-atomic publish transaction. No success receipt would have been returned. There is no startup cleanup or deletion of old files. Users/reviewers must handle explicitly reported incomplete paths; automatic overwrite/retry at that same existing path is rejected. Retrying with a clear new destination creates another unique backup.

There is no public raw-offset command, game operation, telemetry, catalog loading, upload or deployment. The semantic inspector capability remains CanWrite=false and `RequestEdits` remains unsupported. No runtime dependency was added; only the installed .NET base libraries and existing Windows APIs are used. Prior algorithm attribution and license-origin disclosures remain applicable.

## Executed validation

The Release build succeeded with zero warnings/errors. The first complete R4 execution passed:

- 43 R4 cases, including one wrapper invoking retained R3.
- 75 retained R3 cases, including one wrapper invoking retained R2.
- 108 retained R2 cases, including one wrapper invoking the retained R1 copy cases.
- 20 retained R1 copy cases.

This equals 42 + 74 + 107 + 20 = **243 direct cases**, excluding the three nested suite wrappers. R1's superseded 12-byte/opaque-only fixture acceptance is historical, not relabeled as current passing behavior; current checksum/structure replacements run in R2. No failed R4 test execution occurred. Before the first full run, reviewer feedback corrected reentry and damaged-backup diagnostics; those regressions are included.

Actual filesystem coverage includes both flags, one/multiple/no-op replacements, original backup/digest identity, result reinspection, restoration, invalid selection/requests, same-file changes, same-content replacement identities, no-overwrite aliases, directories, ADS/network/reserved devices, hard links, junctions, missing parents and a genuinely ACL-denied output directory. The source write/delete lease is tested during actual output creation. Old targets and baseline originals retain their hashes.

Explicitly labeled injections cover failures before output creation, after creation, during partial write, before flush, readback corruption followed by real hash verification, late creation collision, and primary failure plus failed cleanup. Backup creation/write/flush/verification failures prevent output; failed backup cleanup reports its incomplete path. Tests check retained original backups, exception details, state, retries, pre-existing-file preservation and restoration from an output-failure backup. Injected and real filesystem cases are distinguished in the JSON report. Tests also exercise adversarial request Count/indexer reentry and a caller that catches reentry rejection, plus failed restore's Mismatch state.

`test-results-r4.json` records executed cases, mechanisms, synthetic before/after hashes, historical report hashes and the tested source snapshot. Retained results use `test-results-r3-retained-r4.json`, `test-results-r2-retained-r4.json` and `test-results-copy-retained-r4.json`. Every previous result file was hash-checked unchanged. The two reviewed input hashes are in `spec-inputs-r4.json`.

## Open-source readiness boundary

The full editor is **not yet ready to be presented as a complete open-source editor**. Named game fields, rules, user-facing editing UI and game acceptance remain incomplete/unverified. Separately, final provenance/source review, licensing/notices and package/distribution gates have not been cleared by this module. Missing features alone are not a legal prohibition on publishing a clearly scoped experimental source project: a narrower release may be considered after its own applicable review and disclosure gates. This implementer has not created a public package, commit, upload or deployment.
