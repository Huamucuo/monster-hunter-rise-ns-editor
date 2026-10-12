# Changelog

## 2026-10-12 — unpublished source-only preparation

- Use independent R10 commit `f041cdc8408e9eadc88b05fb95d884e25a8d1198` as the baseline; keep legacy v2.10.11 RC1 separate and unchanged.
- Record the NS device-loan pause: application development and real-device validation are paused.
- Prepare a literal allowlisted source-only snapshot without game catalog content, local test reports, binary releases or Git history. The maintainer approved the new-public-history route and exact MIT grant on 2026-10-12; the existing catalog-bearing history is retained locally and an ordinary push is blocked.
- Add public review documentation, the approved MIT grant and decision record, stronger ignore rules, a manifest boundary check and SHA-pinned synthetic-test CI.
- Guard optional catalog copy items when files are absent; application C# source bytes are unchanged. No new feature, save-format change, UI run, device validation, stable release or publication is claimed.

Original R10 feature/history records remain in FEATURE_MATRIX.md, implementation notes, ISOLATION_LOG.md and README-R10-original.md. Original software evidence dates remain historical; PREPARATION_VALIDATION.md contains only this preparation's fresh software checks.
