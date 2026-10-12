# R11 implementation scope and software checks

R11 extends the independent published source baseline `87a695c` following explicit maintainer authorization for offline name editing, original-template equipment copies, whole-Build copies and reviewed source synchronization. The NS is on loan, so native-device tests remain unavailable. Earlier input manifests, snapshots, implementation notes, preparation reports and original local histories are retained as historical records.

The implementation inputs are exactly the unchanged [FACTS](docs/specs/r11/FACTS.md), [BEHAVIOR](docs/specs/r11/BEHAVIOR.md) and [TESTS](docs/specs/r11/TESTS.md) packets plus the current independent application. The fact author had historical REE-field hypothesis exposure. The implementation role used generated synthetic structures and did not access private saves/probes, game catalogs, other research folders, legacy code/tests or archives. Root review owns independent private-input checks and the final public manifest/synchronization. Shared-machine procedural isolation and GPT assistance do not establish strict clean-room certification, absolute originality or legal clearance.

## Engineering boundaries

- Explicit tag-15 replacements retain exact expected UTF-16LE source bytes. A recursive emitter follows the admitted grammar, preserves logical root/field/array identities and opaque words, retains common original padding and zeroes only newly required padding. No-op serialization returns the source bytes exactly. Reinspection compares every targeted and untargeted logical leaf; coordinate movement from resizing is expected.
- Owner rename resolves both exact unique local routes and validates original/current agreement. Directory use also validates the selected system relationship; malformed, missing or mismatched links reject before mutation. Name input is 1–16 UTF-16 units under a conservative application rule, with no normalization and no claim that this is the game's maximum or every required game mirror.
- Templates must have unchanged original type/ID and identical complete recursive metadata, including classes, identifiers, tags, widths, array element tags/prefixes/counts/modes and leaf shape. Copying stages all current primitive values into distinct current empty records; unrelated values/padding stay exact. Opaque destination values intentionally become template values, with unverified instance semantics. Existing-equipment writers retain their original-only limits.
- Every non-None Build piece requires an explicitly chosen current original template. Fresh identities, stale bindings, unsupported jewels, virtual talismans/sockets, shape failures or insufficient capacity reject the complete set. A successful set uses one staging/history action and never auto-equips. Schema 2 stores numeric selections and opaque source hints; schema 1 remains readable, and hints never waive revalidation.
- Desktop controls use bilingual drafts/previews, numeric fallback without catalogs, actual template pairs and unknown actual socket capacities. Apply actions and experimental warnings remain outside scrolling details.

## Fresh verification commands

[R11_VALIDATION.md](R11_VALIDATION.md) records the fresh scoped results. Compiler/log/report artifacts remain outside public attachments.

Run generated-synthetic checks from a disposable working directory, using a project path selected explicitly for that checkout. Reports, fixtures, preferences and screenshots stay outside the public tree.

```powershell
dotnet build Foundation.csproj -c Release
dotnet build Desktop/RiseWorkshop.csproj -c Release
dotnet run --project Foundation.csproj -c Release -- --self-test-r11
dotnet run --project Foundation.csproj -c Release -- --self-test-history
dotnet run --project Foundation.csproj -c Release -- --self-test-r9
dotnet run --project Foundation.csproj -c Release -- --self-test-r5
dotnet run --project Foundation.csproj -c Release -- --self-test-catalog
dotnet run --project Foundation.csproj -c Release -- --self-test-r10-model
dotnet run --project Desktop/RiseWorkshop.csproj -c Release -- --smoke-r11 <disposable-output-directory>
```

The initial text tests observed the baseline failure “Only primitive data can be staged” in both flags0/flags8 profiles before implementation. The name/template/Build tests then observed missing operations before their implementation; copied talisman display separately observed its original-only read rejection before the read-only presentation change. UI tests first observed missing controls, then exercised the actual name, template-add and complete-set modal callbacks with minimum/large EN/ZH layouts. Failed runs are retained separately from later checks, without public fixture/report attachments.

No application NuGet package, external code/data/asset or network download was added. MHRice attribution and its full MIT notice are retained separately from the own-code MIT grant. Gameplay writes remain WRITE_EXPERIMENTAL; all R11 native rows remain NOT_TESTED. Export/reload, synthetic controls or root byte checks do not imply native load, intended effect, valid duplicate instances or successful game resave.
