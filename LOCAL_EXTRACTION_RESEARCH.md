# Local MSG extraction research

Research-only candidate: https://github.com/dtlnor/REMSG_Converter at 4289554cd0bdd8eba8c61fa0addbcbe9726a7c0f. README and LICENSE were fetched from the official GitHub API and hashed in the auditor record. The repository declares MIT. Its credits identify wwylele/mhrice and ponaromixxx's msg tool for format structure. No converter implementation has been copied, executed or bundled this round.

The documented conversion produces JSON with message entry names, GUIDs, attributes and localized content. That can supply text, but MSG entry keys are not automatically save IDs: a separately reviewed per-family ID/message-key mapping is still required. Parameter data is also needed for truthful slot/rule metadata. This is why merely parsing UTF-16 strings is not a complete local catalog extractor.

Next implementation boundary: accept user-selected extracted .msg files, record version/hash, validate the supported file header and offsets, decode messages locally, map only reviewed Item/Skill/Decoration/Weapon/Armor families, and produce catalog.json with source metadata. No game archive/key handling is authorized by this research note. Use synthetic fixtures plus user-authorized real text files; unknown versions fail clearly. User has reported no such files currently, so the actual local extraction route remains RESEARCHING, not IMPLEMENTED.

LocalGameCatalogProvider currently reads catalog.json only. It must not advertise MSG extraction or independently verified NS names. MHRice output is an optional current name source, not a permanent exclusive architecture.
