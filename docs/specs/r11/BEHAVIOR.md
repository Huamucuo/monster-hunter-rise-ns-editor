# R11 user behavior and acceptance design

The requested result is character renaming, equipment addition and adding a whole Build to the selected character's equipment box, with usable English/Chinese controls, Undo/Redo, modified-copy export and truthful experimental status. Use the current approved public repository at baseline 87a695c; do not change the retained original R10 repository or old editor. Facts come only from FACTS.md/prior admitted facts and the independent application. No implementation layout, API names or code are prescribed.

## Character name

Provide a character-page text draft and explicit Apply/Cancel or reset. Accept 1..16 UTF-16 units as a documented conservative application input policy, NOT a verified game maximum; reject NUL, control characters, whitespace-only strings and unpaired surrogates. Preserve accepted text exactly without hidden trimming/normalization. Display the applied staged name, keep unrelated character fields/cards untouched and synchronize both admitted local owner text fields. In linked directory context synchronize the selected system name in the same history action. Validate every target before any mutation. A failed action or no-op must leave history/redo intact. Export/reload must retain a longer/shorter name, not only same-width cases. Explain single-file mirror limitations visibly.

Implement the smallest extension to the independent parser/serializer necessary for explicit tag-15 replacement. Preserve original text/raw leaf data when unchanged, original root/object/array identifiers and counts, opaque header words, unknown primitive values and compatible nonzero padding. Rebuild alignment only where lengths force a change; preserve common padding bytes and make newly required padding zero. No edit returns byte-identical source. Reinspect output and compare logical structural identities/untargeted values; coordinate shifts are expected for text resizing. Reuse the existing original-only backup/output workflow, do not add a second unsafe file writer.

## Add equipment

Equipment page offers Add from template. Select any original occupied source record, choose quantity within available empty capacity and preview destination positions and copied settings. Numeric fallback works without a catalog. Optional names enrich the chooser without supplying write permission. Weapon/armor/talisman source types supported where shapes match. Default new copies clear the three admitted decoration IDs; optionally copying source decorations must be explicit and described as experimental. Preserve copied paired talisman values and all unreviewed fields. Do not invent socket/rarity/upgrade defaults.

All target positions must currently be empty (type0/id0), distinct, metadata-compatible and within capacity. Resolve everything before Apply. One Apply is one Undo/Redo group, and newly added records appear in the equipment list. Existing-equipment APIs must keep their conservative rules rather than accidentally treating new records as unrestricted existing templates. An occupied target, no capacity, changed template or malformed shape produces a useful failure and no partial staging.

## Build to box

Add a normal selectable mode for an original owned equipment template at each Build position. This must work without an external catalog, including a template-based talisman position and None. Preserve the existing catalog-based offline/virtual choices. Clearly identify template-based talisman vs virtual planning; retain actual source pair data for display, and do not invent actual socket levels. Numeric ID/record selection is sufficient where part metadata is absent; positions are planning labels, not an auto-equipped loadout.

Store only necessary numeric selections and, if needed, an opaque source digest/template binding in a versioned portable plan. Never store player names, paths, decoded save data or catalog text. Existing schema-1 plans remain readable; a changed schema must be versioned. Source bindings are hints requiring full current-source revalidation, not trust grants. A mismatched/stale loaded plan must be resolvable by explicitly choosing a current template.

Provide Add set to equipment box with a fixed visible action and preview: selected character, requested pieces, source templates, destination records and effect of adding copies. It stages all selected non-None pieces in one group and never auto-equips. Resolve an explicitly chosen original template for every piece; an unowned catalog identity, unresolved/mismatched source binding, unsupported planned decoration or virtual socket request blocks the whole operation with precise reasons. Offer current same-kind/same-ID template choices rather than silently choosing a different configuration. No piece may be silently omitted. Capacity failure leaves all stages/history unchanged. Success refreshes the equipment list and summary; export/reload contains all copies.

Reference catalog values remain UNVERIFIED_MAPPING and do not widen save edits. Planned jewels may only be empty or the admitted source's original same-position ID; planned coupled talisman pairs must use original observed pairs. An existing template talisman copies its actual raw auxiliary data; virtual talisman output is unavailable rather than ignored. User UI must make these boundaries clear before applying.

## Complete deliverable and GitHub

| Required result | Operation and evidence |
|---|---|
| Rename | Draft -> Apply -> linked export -> reload; longer, shorter, CJK and non-BMP names, unrelated cards preserved |
| Add equipment | Select original source template -> add to empties -> Undo/Redo -> export/reload; weapon/armor/talisman |
| Write Build | Select template pieces including optional None/talisman -> add all -> one Undo -> reload complete set |
| UI | Bilingual actual controls at minimum/large window; confirmation visible and details separately scroll |
| Source boundary | Original saves/old project unchanged; permitted specs/input hashes, notices, GPT/history retained |
| Delivery | .NET builds, related synthetic tests, independent byte checks, current manifest/allowlist and public-source CI |

Update FEATURE_MATRIX to WRITE_EXPERIMENTAL for actual new gameplay writers, not VERIFIED. Add NOT_TESTED game-validation rows and concrete manual steps for rename/template add/Build; no synthetic results promote them. Update README/ROADMAP/SOURCE_POLICY/CONTRIBUTING/PROVENANCE to state offline development resumed by explicit request while NS/device testing remains unavailable. Historical pause and input snapshots remain accurate historical records, not erased. No extra runtime packages, assets, network downloads, backups test batches or whole-project legal audit are needed.

Implementer must not push or publish. Root reviews, independently tests, rebuilds public manifest and synchronizes the authorized reviewed source changes to GitHub. Keep full third-party notices and own MIT grant. Do not add game catalogs/real-save artifacts to the repo or old histories to the remote.
