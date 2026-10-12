# Rise Structural Workshop

An experimental Windows desktop editor by 花木错, implemented with GPT assistance. It opens explicit local files or the four recognized slot/system filenames in a selected folder. Nothing is auto-loaded at startup.

## Build and launch

Requires the .NET 10 SDK to build and the .NET 10 Windows Desktop Runtime to run. No third-party NuGet package is required.

```powershell
dotnet build Desktop\RiseWorkshop.csproj --configuration Release
.\Desktop\bin\Release\net10.0-windows\RiseWorkshop.exe
```

The executable launches a WinForms desktop window, not a browser. Keep the generated application files together; this is a framework-dependent build, not a single-file distribution package.

## Available actions

- Open `data001Slot.bin`, `data002Slot.bin`, `data003Slot.bin` and `data00-1.bin` from an explicitly selected folder. Missing or unsupported candidates do not hide readable ones. Recognition does not prove a character occupies a slot or authenticate the game/version.
- Read the character name and available ranks; stage money and points within conservative original-value bounds.
- Read and filter numeric item IDs, change quantities, add an ID already present in the original inventory into an empty record, or empty an existing record. These operations preserve capacity and record order; combined quantity per ID cannot exceed its original total. Reduce/delete quantity before redistributing it into an empty record.
- Read numeric weapon/armor/talisman IDs and decoration IDs. On an existing talisman, copy a coupled skill-ID/level pair already observed on a talisman in the selected source into either skill position.
- Apply, Undo or Clear staged changes. In linked folder mode, a money action and its matching system preview use the same Undo/Clear action.
- Export new copies with verified original backups. Folder export includes all four recognized known candidates; untouched candidates are byte-identical copies. Missing/unsupported files and per-file failures are listed. Multi-file export is not atomic and an incomplete result is not a complete bundle.
- Restore the original backup associated with the selected slot's successful export into another new file. Source files and existing destinations are never overwritten.
- Browse the structure in pages, navigate parent/child events, search opaque IDs or local text, and inspect primitive bytes in the separate read-only research tab.

When opening a slot file by itself, the system cached preview is not updated. Opening another folder clears the old selection and staged work after the desktop discard prompt. Opening an exported file starts a fresh explicit-file context.

## Current limits

Writes are **experimental** and game acceptance remains unverified. Bundled MHRice English/Chinese name candidates are available through an explicit **Show unverified name candidates** toggle, off by default. These external names are not verified NS mappings. Additional local catalogs may be selected explicitly; numeric IDs remain the default. See CATALOG.md for the schema and declaration-only trust policy. Adding an arbitrary new item ID, creating equipment, editing weapons/armor, rarity, augmentation, socket-related data or variable-length records is not implemented. Names/ranks remain read-only. Talisman choices do not guarantee legal game rolls. A checksum match detects certain errors; it is not authenticity or proof of game validity.

Field-name hypotheses had historical-source exposure, including REE Save Editor references, before independent byte measurements were admitted. That historical reference is retained; this is not a claim of independent format discovery. This implementation used reviewed fact packets rather than historical editor code. MurmurHash3 x86_32 is Austin Appleby's public-domain algorithm; it was not invented by this project. See `ISOLATION_LOG.md` and the implementation notes for procedural limitations, exact scope and attribution. No strict legal clean-room, absolute originality or final licensing-clearance claim is made.

The full requested editor is still incomplete. A narrower experimental-source release is a separate decision; provenance, notices and packaging review remain outside this implementation snapshot.

## Focused checks and synthetic UI smoke

```powershell
dotnet run --configuration Release -- --self-test-r5
.\Desktop\bin\Release\net10.0-windows\RiseWorkshop.exe --smoke .\smoke-local
```

Smoke mode generates its own synthetic fixture, renders four tabs at 1120×760 and 1500×940, checks selection retention, writes screenshots/layout results and exits. It does not load a real save. Normal startup accepts no auto-load path. Historical reports are preserved; `test-results-r9-workspace-regression.json` covers the current focused workspace/domain checks; `test-results-r5.json` is retained historical evidence.


## R6 game-validation phase

Use **Game validation…** or the CLI workflow in [GAME_VALIDATION.md](GAME_VALIDATION.md) to prepare a no-edit baseline and independent cases, record human game evidence and compare a returned four-file bundle. Generated backups remain outside each import folder. No game operation or automatic verification promotion occurs. The repository [game validation matrix](GAME_VALIDATION_MATRIX.md) records pending actual evidence separately from synthetic tests.

Equipment now has a separate read-only field view. The presentation-name interface uses numeric fallback and requires namespace/source/license metadata for optional caller-supplied labels; the later MHRice provider adds separately attributed external candidates, with no legacy catalog imported. No new game write capability was added in this phase.

## Interface language

Use the visible **Language / 语言** choice at the top to switch between English and Simplified Chinese. First launch defaults to English. Your choice is remembered in `%LOCALAPPDATA%\RiseStructuralWorkshop\preferences.json` (`language`: `en` or `zh-CN`); removing that preference file restores the English default. Switching updates the current controls in place and preserves selected records, pending edits, numeric IDs and save bytes. Player text, filenames, paths and raw diagnostic details remain original. Technical structure details may remain English.

Synthetic `--smoke <folder>` runs use `<folder>/test-preferences.json`, never the real preference file. They render both languages and check staged-edit/selection retention, preference memory, placeholder round trips and raw-text preservation.

## Desktop layout

The window groups opening/export actions separately from secondary tools, with the language label and selector kept together at the right. Character, inventory, equipment and advanced structure use one tab row. With no file selected, a welcome panel replaces editing controls and empty navigation. Character information and currency use separate cards; grids use consistent spacing and row colors. Loaded-state Undo/Clear positions remain stable as staging changes. This layout changes no save behavior or gameplay validation status.



## R9 existing-record editing

Inventory **Split / move / merge…** transfers positive quantities to an empty or same-ID record without expanding totals or arrays. Each final stack is bounded by the largest original same-ID stack; this is an application bound, not a game maximum.

**Edit equipment draft…** shows three decoration positions for existing weapons/armor/talismans. Each may remain original or become zero; restoration uses only that record's original same-position ID. Socket capacities and jewel-stock effects are unknown. Talismans can apply both original-observed skill/level pairs together. Widgets do not stage until Apply; Cancel discards them and one Undo reverses the applied action. Missing optional sections remain independently unavailable.

Name searches cover all available languages, and equipment provider-category filters follow the unverified-candidate toggle. Numeric IDs and write permissions do not depend on labels. New gameplay rows remain NOT_TESTED; see GAME_VALIDATION.md for manual steps. Run `dotnet run -c Release -- --self-test-r9` and desktop `--smoke-r9 <output-folder>` for focused synthetic checks.

## Offline Build and reference equipment data

**Build / 配装** opens without a save. Explicitly enable unverified candidates to choose catalog references. Select one of seven positions, edit a detached piece and confirm it; Cancel leaves the plan unchanged. Weapon category and correct armor-part choices are separate. A virtual talisman is a planning configuration only, with two catalog skill/level pairs and three offline slots. Decorations can be found by multilingual name, numeric ID or contributing skill and are filtered against the plan slot. Invalid retained placements remain visible and are excluded from totals.

The summary shows reference defense/resistances, weapon base attack/affinity and aggregate skills with declared maximum/excess. Unknown inputs produce partial results; this does not simulate upgrades, augmentation, damage or set bonuses. Equipment information and catalog details distinguish observed save IDs from optional external base parameters. Every catalog mapping stays UNVERIFIED_MAPPING.

Open/Save handles a separate `rise-offline-build` version 1 JSON (at most 1 MiB), containing title and numeric choices only. Save requires a new filename; it does not silently replace an existing plan. Unsaved plans require discard confirmation. Adding a plan to a save remains unavailable.

**Redo** replays an undone group. A successful new edit discards the redo branch; failed/no-op edits retain it. **Staged changes…** displays current original/staged differences, with opaque fields kept raw/read-only. Clear is itself undoable/redoable; selecting a different context resets history. Original save files remain unchanged.

Focused commands: `--self-test-r10` (planner/metadata), `--self-test-history` (8 history groups), desktop `--smoke-r10 <new-output-folder>` (synthetic bilingual planner/history UI). Test preferences and generated plan files stay inside that output folder.
