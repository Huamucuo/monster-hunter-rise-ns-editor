# R11 fresh software verification

These are fresh software checks executed on 2026-10-12–13 against the independent R11 implementation. They use newly generated synthetic structures and optional synthetic catalog labels only. No private save, actual game catalog, legacy implementation or research probe was accessed by the implementer. This record does not replace earlier dated reports or promote native-game evidence.

| Check | Fresh result |
|---|---|
| Foundation Release build | PASS; 0 warnings, 0 errors |
| Windows desktop Release build | PASS; 0 warnings, 0 errors |
| R11 model (`--self-test-r11`) | 14 groups, 0 failures |
| Retained grouped history (`--self-test-history`) | 8 groups, 0 failures |
| Retained R9 domain operations (`--self-test-r9`) | 7 groups, 0 failures |
| Retained R5 workspace/domain (`--self-test-r5`) | 12 cases, 0 failures |
| Catalog-independent catalog policy (`--self-test-catalog`) | 7 groups, 0 failures |
| Retained R10 synthetic planning (`--self-test-r10-model`) | 4 groups, 0 failures; excluded actual-resource case not run |
| Retained R2 serializer prerequisites | 108 cases, 0 failures |
| Retained R3 primitive serializer inside R4 | 75 cases, 0 failures |
| Retained R4 export aggregate | 43 cases, 0 failures |
| R11 actual desktop controls (`--smoke-r11`) | 16 groups, 0 failures; EN/ZH at 1120×760 and 1500×940 |
| Retained R10 review controls (`--smoke-r10-review`) | 9 groups, 0 failures |
| Retained R9 equipment controls (`--smoke-r9`) | All 7 result groups passed, including draft/cancel/Apply/Undo and localization checks |

The retained serializer/export aggregate used freshly executed synthetic copy/report prerequisites in a separate disposable directory. It did not import, relabel or overwrite historical private report files. Nested runners share cases; these counts are not a count of unique game-valid scenarios.

R11 model coverage includes flags0/flags8 growing/shrinking/mixed text output, strict expected UTF-16 bytes, no-op identity, common nonzero padding and opaque header/leaf preservation, owner/system correlation and failed/no-op history, original weapon/armor/talisman sources, current staged settings, metadata/capacity/stale/occupied/duplicate-target rejection, copied-pair read-only presentation, complete seven/six-piece staging, schema1/2, stale bindings and whole-action virtual/jewel/None failures.

Owner/system associations explicitly compare original/current UTF-16LE bytes. A synthetic malformed-original negative case confirms that the existing strict reader rejects unpaired surrogates before name association. This is contract clarification, not evidence of a name collision in an accepted legitimate document or a game wrong-name fix. Read-only TextAt display behavior is unchanged.

Actual desktop checks use the main name Apply/reset, equipment Add modal and seven piece-edit/whole-set confirmation callbacks. They assert that the name input itself, fixed actions and experimental/mirror warnings are within visible bounds; verify stage/Undo/Redo and language/draft retention; and close each owned controller. The smoke runs inside a real persistent application message loop, preserving its UI synchronization context. Initial missing controls, clipped footer warnings, hidden name input and the message-loop/lifetime failures were observed before their respective fixes; failed evidence is retained separately from green evidence. No assertions were removed to hide failures.

Commands and boundaries are in [IMPLEMENTATION_NOTES_R11.md](IMPLEMENTATION_NOTES_R11.md). Synthetic inputs, output/backup copies, compiler logs, screenshots, preferences and detailed reports stay outside public attachments. Root review separately owns authorized private-input regression, independent byte comparisons, the final manifest and protected-branch source synchronization.

Gameplay writers remain **WRITE_EXPERIMENTAL**. Every new native-validation row remains **NOT_TESTED**: no NS load, effect, game resave or native reread was performed. Cloned opaque instance semantics, complete game-name mirrors/rules and other game versions remain unverified. Other DPI/accessibility environments and stable binary distribution are outside these checks.
