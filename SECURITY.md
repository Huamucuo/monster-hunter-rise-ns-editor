# Security reporting and scope

This unpublished experimental candidate has no stable supported release or established response-time promise. Device-dependent development and validation are paused while the maintainer's NS is lent out.

Useful reports include malformed-input crashes/resource exhaustion, unexpected source/destination writes, backup/restore confusion, unsafe file selection, unintended network activity and disclosure of private save data. Keep a report tied to the exact source commit/manifest and a synthetic reproducer where possible.

Do not publish actual saves, catalog datasets, player names, personal paths, account identifiers or keys in an issue or screenshot. A private reporting channel must be explicitly established by the maintainer before publication. Until then, prepare a sanitized draft for the maintainer; this document provides no invented email address and does not authorize external contact.

No security audit, exploit-resistance guarantee, legal clearance or game-validity certification is asserted. Original source files should remain unchanged during normal application exports, but multi-file exports are not atomic and higher-ancestor filesystem race limitations remain disclosed in the implementation notes. Preserve private originals and independent backups; passing software checks do not make experimental edits safe for every game/version.

CI uses read-only repository permission, SHA-pinned actions and synthetic data. It does not publish, upload game data, require secrets or operate a console/game. Publication/license approval is independent of any security report.
