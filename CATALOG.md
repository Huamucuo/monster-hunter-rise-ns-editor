# Optional local catalog and conservative editing

The bundled MHRice resource provides 7675 English/Simplified Chinese external name candidates (1771 items, 147 skills, 244 decorations, 3953 weapons, 1560 armor entries). Ordinary display defaults to numeric IDs. Explicitly enable **Show unverified name candidates** to display and search candidates. All bundled mappings remain UNVERIFIED_MAPPING; numeric overlap does not verify Nintendo Switch semantics. See DATA_SOURCES.md and THIRD_PARTY_NOTICES.md for provenance and rights limitations.

Choose **Names and sources…**, then **Load local catalog…** and explicitly select a local directory containing `catalog.json`. The reviewed bundled resource is registered at startup; additional local directories are selected explicitly. No catalog is downloaded or uploaded. The candidate toggle defaults off each launch. The browser supports name/ID search, kind/category filters, source-observed ownership and ID/name ordering. Item/equipment pages also search imported labels while retaining numeric record targets. Metadata and declared bounds appear in the selected entry's details. Unknown ranges stay unknown.

Minimal synthetic schema example:

```json
{
  "SchemaVersion": 1,
  "Entries": [{
    "Kind": "Item",
    "Id": 1,
    "Names": { "en": "SYNTHETIC example", "zh-CN": "合成示例" },
    "Category": "Synthetic",
    "Origin": { "Source": "User-provided provenance", "License": "User-provided permission", "Edition": "Unknown" },
    "DeclaredStatus": "KNOWN_ITEM",
    "DeclaredMaximumQuantity": null,
    "DeclaredMaximumSkillLevel": null,
    "RequiredSlotLevel": null,
    "SkillRelations": []
  }]
}
```

Kinds: Item, Weapon, Armor, Skill, Decoration, Rampage. EquipmentKind is weapon=1 or armor=2 only (inferred from the explicit catalog kind when omitted), never reused across namespaces. Unknown enum values and duplicate keys fail. Names are locale-keyed, falling back to English, another supplied label, then numeric ID. Every declaration remains untrusted local metadata: VERIFIED_ITEM self-declarations become effective KNOWN_ITEM, missing source/license becomes UNKNOWN_ITEM, and UNSUPPORTED remains unsupported. Nothing in imported JSON grants independent verification or wider write permissions. Declared quantity/skill/slot bounds are visibly separate from conservative source bounds.

Current normal edit policy:

- Money and points: zero through the original nonnegative value. This is a conservative operation envelope, not a measured game maximum. Missing/negative money stays read-only.
- Items: existing same-source IDs only. The sum across every staged record for an ID cannot exceed that ID's original positive total. First reduce/delete quantity to free budget, then fill an existing empty record; copying into multiple records does not expand the total. No arbitrary new IDs. Negative/malformed original inventory blocks quantity/add; an anomalous selected record cannot be deleted through the normalizer.
- Talisman editing still accepts only original observed coupled skill/level pairs. No new game ranges were inferred.
- The ordinary desktop structure page is read-only. The experimental primitive replacement API remains isolated for development/test workflows, not exposed as a normal range-validated edit action.
- Character-name writing is unavailable; no fixed buffer, character limit or mirror synchronization was inferred.

These bounds do not prove that original values are legal in the game. Catalog claims cannot loosen them. User-visible numeric controls use these conservative bounds rather than presenting int.MaxValue as a game maximum.

`GameCatalog.DecorationsForKnownSlot(level, ownedIds)` is a presentation-only API. It accepts explicitly provided basic levels 1..4 and only entries with declared requirements 1..4 within that value. Null/zero/outside-range inputs yield no choices. Actual save socket meaning is not established; no socket/decoration writer is enabled. A filtered claim is not verified legal equipment.

Historical increased-value exports and EXPORTED game-validation evidence remain unchanged. The new policy may reject an old prepare plan: in particular, a fresh one-operation add case has no freed quantity budget. Existing exported cases can still be compared with returned game saves. Do not rewrite historical manifests as if they used the new rules.

Focused synthetic checks: `dotnet run --configuration Release -- --self-test-catalog`. Previous test reports remain historical and are not overwritten by this new policy suite.

## Provider contract

IGameCatalogProvider separates namespace/ID lookup from presentation. LocalGameCatalogProvider and MhriceCatalogProvider both supply unverified candidates. CatalogProviderNames reserves priority for independently verified local, then verified external, then local/external candidates, then raw IDs; issuing VERIFIED_NS_MAPPING is currently rejected because no admitted evidence supports it. Local JSON status cannot self-authorize verification. Candidate labels and details preserve numeric ID, equipment kind, source, license and edition. Candidate display never changes numeric edit targets or conservative bounds. No local .msg reader is implemented.

Run --self-test-providers for focused synthetic/provider and admitted-resource checks. Actual name resources in these checks are not real save inputs.

