# Rise Structural Workshop — R11 development preview

> **Offline application development resumed by explicit maintainer request on 2026-10-12. Native-device testing remains unavailable while the Nintendo Switch is on loan.**
> 用户已明确要求恢复离线软件开发；NS 设备暂借他人，实机验证仍不可进行。

An experimental Windows desktop save editor by 花木错, developed with OpenAI Codex / GPT assistance. This is the published **source-only development preview**, extended from public baseline `87a695c` using the reviewed [R11 facts](docs/specs/r11/FACTS.md), [behavior](docs/specs/r11/BEHAVIOR.md) and [tests](docs/specs/r11/TESTS.md). The retained independent R10 baseline is `f041cdc8408e9eadc88b05fb95d884e25a8d1198`. This is separate from the legacy v2.10.11 application and does not claim stable or complete-editor readiness.

**Source-only development preview.** The maintainer approved publication from this reviewed allowlist and the MIT grant on 2026-10-12. This repository starts **new public history from that source snapshot**; every original local commit remains preserved. The original `clean/foundation` history contains excluded game-derived catalog content and is not imported or pushed here. Source publication is separate from a stable release, binary distribution, public service or game/device validation. See [PUBLICATION_BOUNDARY.md](PUBLICATION_BOUNDARY.md).

## What this source currently implements

- Explicit local file/folder selection; no save is auto-loaded at startup. Supported structural profiles remain experimental and do not authenticate game identity or version.
- Experimental character renaming with explicit draft Apply/reset, exact text preservation, both admitted local owner mirrors and a corresponding system mirror in validated directory context. The conservative 1–16 UTF-16-unit input policy is an application rule, not a verified game maximum. A malformed/mismatched directory system link blocks renaming; explicit single-file use clearly reports its local-only limitation. Ranks remain read-only.
- Conservative original-value currency staging and fixed-record inventory quantity/transfer operations; existing-equipment decoration clearing/original-position restoration and original-observed coupled talisman skill/level choices.
- Add weapon, armor or talisman copies from an unchanged original type/ID template into existing empty equipment records. Preview quantity/destinations and copied settings. Decorations clear by default; copying source decorations requires explicit experimental opt-in. Complete recursive metadata must match, opaque template values are copied, and unknown fields outside destinations stay unchanged. Game instance validity remains unverified.
- Grouped Apply, Undo, Redo, Clear and staged-change review. Export creates separate copies and verified original backups; multi-file export is not atomic.
- A seven-position Build planner with original owned-template choices for weapon, five armor labels and an actual template talisman or None, without a catalog. Add set previews every requested piece and stages the complete set in one history group. It never auto-equips. Fresh unowned identities, stale bindings, unsupported planned jewels, virtual talismans/sockets or insufficient capacity block the whole action. Schema-1 offline plans remain readable; template plans use schema 2 with numeric choices and opaque source hints that require current-source validation.
- A Windows WinForms interface with English and Simplified Chinese options, not a browser application.

Save writes are **WRITE_EXPERIMENTAL**. A checksum match, successful export/re-read, passing synthetic test or integer ID overlap is not native-game acceptance. The requested full editor remains incomplete. [FEATURE_MATRIX.md](FEATURE_MATRIX.md) and [GAME_VALIDATION_MATRIX.md](GAME_VALIDATION_MATRIX.md) preserve exact feature and evidence boundaries.

## Catalog-free public scope

This preview excludes actual game catalogs, names/parameter datasets, saves, exports/backups, screenshots, local reports, reference archives, PDBs, caches, game binaries and all original Git history. It preserves the source code, synthetic fixture builders, admitted-input/source identity records, MHRice attribution and its full MIT notice. Source/provenance hashes are metadata, not bundled game data or an ownership certificate.

Optional catalog copy items remain guarded when their files are absent. Numeric original-template choices support equipment addition and all Build positions without any catalog. A separately selected lawful local catalog can enrich names and reference totals, all still **UNVERIFIED_MAPPING**; it supplies no additional write permission. No catalog is supplied or downloaded. See [data/mhrice/README.md](data/mhrice/README.md) and [CATALOG.md](CATALOG.md).

## Build and focused checks

Use Windows and the .NET 10 SDK. This preparation uses SDK 10.0.102. No extra application NuGet package is declared; a normal build requires the matching platform reference packs. The desktop is framework-dependent and needs the .NET 10 Windows Desktop Runtime to run.

```powershell
dotnet build Foundation.csproj --configuration Release
dotnet build Desktop/RiseWorkshop.csproj --configuration Release
```

The catalog-independent suites below generate their own synthetic inputs and write disposable outputs. Run them from a separate working directory to retain prior reports:

```powershell
dotnet run --project Foundation.csproj -c Release -- --self-test-history
dotnet run --project Foundation.csproj -c Release -- --self-test-r9
dotnet run --project Foundation.csproj -c Release -- --self-test-r5
dotnet run --project Foundation.csproj -c Release -- --self-test-catalog
dotnet run --project Foundation.csproj -c Release -- --self-test-r10-model
dotnet run --project Foundation.csproj -c Release -- --self-test-r11
```

`--self-test-r10` and `--self-test-providers` include assertions against the excluded actual MHRice resource, so they are **not** standalone public CI checks. `--self-test-r10-model` runs its retained synthetic planning cases without that resource case. The opt-in `--smoke-r11 <disposable-output-directory>` desktop mode drives actual name, template-add and seven-piece Build controls in English/Chinese at minimum/large sizes; it generates only synthetic files, preferences and screenshots. Those outputs are not public attachments. Other historical runners can require earlier reports or additional inputs.

[PREPARATION_VALIDATION.md](PREPARATION_VALIDATION.md) reports only fresh checks on this preview, with exact commands and limits. The legacy v2.10.11 record of 83 passes and the original R10 results dated 2026-10-05 remain historical; they are not today's tests and are not interchangeable.

[IMPLEMENTATION_NOTES_R11.md](IMPLEMENTATION_NOTES_R11.md) records the new engineering scope and fresh-check commands without private reports or game data. Software exports/reloads remain separate from native acceptance.

## Remaining work after the device returns

Native game load/effect/resave/editor reload evidence and game/version-specific validity remain outstanding for every new writer. Current-equipped mappings, fresh equipment identities, virtual socket output, augmentation, buddy and appearance editing remain unsupported. Optional reference catalog parameters do not supply authority to write those fields. Native testing needs a separately authorized device scope after the console returns; see [ROADMAP.md](ROADMAP.md) and the concrete R11 manual steps in [GAME_VALIDATION_MATRIX.md](GAME_VALIDATION_MATRIX.md).

## License and provenance

Own-code material the publisher is entitled to license is available under **MIT**, approved by the maintainer on 2026-10-12. The exact grant is in [LICENSE](LICENSE); [LICENSE-PROPOSAL.md](LICENSE-PROPOSAL.md) preserves that decision and text. Third-party material retains its own applicable notice; the full MHRice MIT text remains in `licenses/MHRice-MIT.txt`.

Historical REE-derived field hypotheses were consulted by the measurement/review role before fact packets were admitted. The independent implementation used reviewed packets rather than importing the legacy editor code, under procedural session isolation. This was not an OS-enforced isolation boundary, independent historical format discovery, a legally certified clean room or proof of absolute originality. GPT assistance and those limits remain disclosed in [PROVENANCE.md](PROVENANCE.md), [ISOLATION_LOG.md](ISOLATION_LOG.md), the implementation notes and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

MHRice's code/output permission and a project MIT grant do not relicense Capcom/Nintendo game content or establish all missing authorship evidence. This unofficial project is not affiliated with or endorsed by those companies. The [original R10 README](README-R10-original.md) is retained explicitly as historical documentation, not the current packaging or validation claim.

Read [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md) before sharing a report. Do not attach private saves, catalogs, player details or personal machine paths to public discussions.
