# Rise Structural Workshop — R10 Build in Public preview

> **PAUSED — the maintainer's Nintendo Switch is lent to someone else. Application development and real-device validation are currently paused.**
> NS 设备暂借他人，目前开发与实机验证暂停。

An experimental Windows desktop save editor by 花木错, developed with OpenAI Codex / GPT assistance. This directory is an **unpublished source-only review candidate**, based on R10 commit `f041cdc8408e9eadc88b05fb95d884e25a8d1198`. It is not the legacy v2.10.11 application, a stable release, or a declaration that all requested functions are complete.

**Source-only development preview.** The maintainer approved publication from this reviewed allowlist and the MIT grant on 2026-10-12. This repository starts **new public history from that source snapshot**; every original local commit remains preserved. The original `clean/foundation` history contains excluded game-derived catalog content and is not imported or pushed here. Source publication is separate from a stable release, binary distribution, public service or game/device validation. See [PUBLICATION_BOUNDARY.md](PUBLICATION_BOUNDARY.md).

## What this source currently implements

- Explicit local file/folder selection; no save is auto-loaded at startup. Supported structural profiles remain experimental and do not authenticate game identity or version.
- Character-name/rank reading, conservative original-value currency staging, and fixed-record inventory quantity/transfer operations. Names and ranks remain read-only.
- Existing-equipment decoration clearing/original-position restoration and original-observed coupled talisman skill/level choices. It does not create equipment or expand records.
- Grouped Apply, Undo, Redo, Clear and staged-change review. Export creates separate copies and verified original backups; multi-file export is not atomic.
- A detached seven-position offline Build planner with explicit local JSON Open/Save. It cannot add a plan to a save. Reference totals require separately supplied optional catalog data.
- A Windows WinForms interface with English and Simplified Chinese options, not a browser application.

Save writes are **WRITE_EXPERIMENTAL**. A checksum match, successful export/re-read, passing synthetic test or integer ID overlap is not native-game acceptance. The requested full editor remains incomplete. [FEATURE_MATRIX.md](FEATURE_MATRIX.md) and [GAME_VALIDATION_MATRIX.md](GAME_VALIDATION_MATRIX.md) preserve exact feature and evidence boundaries.

## Catalog-free public scope

This preview excludes actual game catalogs, names/parameter datasets, saves, exports/backups, screenshots, local reports, reference archives, PDBs, caches, game binaries and all original Git history. It preserves the source code, synthetic fixture builders, admitted-input/source identity records, MHRice attribution and its full MIT notice. Source/provenance hashes are metadata, not bundled game data or an ownership certificate.

The desktop project has only a packaging change: optional catalog copy items are guarded when their files are absent. Application `.cs` files are unchanged. Existing runtime code already uses numeric fallback without a bundled catalog. Name/reference equipment candidates and useful real-equipment Build choices need a separately selected lawful local catalog; this preparation neither supplies nor downloads one. See [data/mhrice/README.md](data/mhrice/README.md) and [CATALOG.md](CATALOG.md).

## Build and focused checks

Use Windows and the .NET 10 SDK. This preparation uses SDK 10.0.102. No extra application NuGet package is declared; a normal build requires the matching platform reference packs. The desktop is framework-dependent and needs the .NET 10 Windows Desktop Runtime to run.

```powershell
dotnet build Foundation.csproj --configuration Release
dotnet build Desktop/RiseWorkshop.csproj --configuration Release
```

Do not launch the application or run real-save tests as a side effect of preparing source. The four catalog-independent suites below generate their own synthetic inputs and write only disposable outputs:

```powershell
dotnet run --project Foundation.csproj -c Release -- --self-test-history
dotnet run --project Foundation.csproj -c Release -- --self-test-r9
dotnet run --project Foundation.csproj -c Release -- --self-test-r5
dotnet run --project Foundation.csproj -c Release -- --self-test-catalog
```

Run them in a disposable working directory when preserving earlier local reports matters. `--self-test-r10` and `--self-test-providers` include assertions against the excluded actual MHRice resource, so they are **not** standalone public CI checks. Desktop smoke modes create UI fixtures/screenshots and are also excluded from this preparation. Other historical runners can require earlier reports or additional inputs; do not assume every runner is catalog-independent.

[PREPARATION_VALIDATION.md](PREPARATION_VALIDATION.md) reports only fresh checks on this preview, with exact commands and limits. The legacy v2.10.11 record of 83 passes and the original R10 results dated 2026-10-05 remain historical; they are not today's tests and are not interchangeable.

## Remaining work after the device returns

Native game load/effect/resave/editor reload evidence, game/version-specific validity, equipped-reference mappings, equipment creation, name writing, augmentation, buddy and appearance editing remain outstanding. Optional reference catalog parameters do not supply authority to write these fields. Resume only through a separately agreed development and device-validation scope; see [ROADMAP.md](ROADMAP.md).

## License and provenance

Own-code material the publisher is entitled to license is available under **MIT**, approved by the maintainer on 2026-10-12. The exact grant is in [LICENSE](LICENSE); [LICENSE-PROPOSAL.md](LICENSE-PROPOSAL.md) preserves that decision and text. Third-party material retains its own applicable notice; the full MHRice MIT text remains in `licenses/MHRice-MIT.txt`.

Historical REE-derived field hypotheses were consulted by the measurement/review role before fact packets were admitted. The independent implementation used reviewed packets rather than importing the legacy editor code, under procedural session isolation. This was not an OS-enforced isolation boundary, independent historical format discovery, a legally certified clean room or proof of absolute originality. GPT assistance and those limits remain disclosed in [PROVENANCE.md](PROVENANCE.md), [ISOLATION_LOG.md](ISOLATION_LOG.md), the implementation notes and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

MHRice's code/output permission and a project MIT grant do not relicense Capcom/Nintendo game content or establish all missing authorship evidence. This unofficial project is not affiliated with or endorsed by those companies. The [original R10 README](README-R10-original.md) is retained explicitly as historical documentation, not the current packaging or validation claim.

Read [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md) before sharing a report. Do not attach private saves, catalogs, player details or personal machine paths to public discussions.
