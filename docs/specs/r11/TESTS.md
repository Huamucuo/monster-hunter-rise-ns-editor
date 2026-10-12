# R11 focused regression requirements

Use only newly generated synthetic data, independent source and reviewed fact packets. Save fixtures contain no player information or game resources. Add tests before production implementation; preserve historical reports.

- Text serializer: flags0 and flags8, shrinking/growing strings across alignment boundaries, two owner fields plus corresponding system text, mixed text/primitive changes, nonzero padding/opaque words and untouched text/primitive values, strict malformed UTF-16/expected bytes, byte-identical no-op, unchanged root/field/array logical structure. In ordinary text access/changes/Undo/Redo/Clear the staged text must be reflected.
- Rename: exact unique required paths; unrelated same-text card/other-slot name unaffected; linked mirrors; mismatched/missing owner/cache rejection; single-file warning; range1..16 UTF16 units vs CJK/non-BMP; invalid/no-op action preserves existing staging and redo.
- Template add: original weapon/armor/talisman, source-to-empty full leaf mapping, unchanged source/unknown external fields, staged template values, default empty decorations, multiple copies, no remaining capacity, occupied or metadata-mismatched target, nonoriginal/unresolved identity. Atomic history and independent export/reload.
- Build: all seven source positions and six without talisman; template selection without a real catalog; multiple matching identities demand explicit choice; None; whole-set capacity/rejection atomicity; portable old/new schema, stale source binding; no silent virtual talisman/jewel omissions; no auto-equipping; single Undo/Redo for complete set.
- Actual desktop: English/Chinese name action, template-add action and whole-set action through real controls; minimum window/footer/warning visibility, selection/state retention and no per-keystroke full parse. Synthetic output/prefs stay outside public attachments.
- Retained history/R9/domain/catalog CI suites plus required existing serializer/export regressions affected by the API extension. No arbitrary repeated safe-export cases.

Root independently uses authorized samples to test structure/export/reload and unchanged source. Neither the implementer nor public CI accesses these samples. Real console acceptance remains NOT_TESTED.
