# Preparation validation

This record applies only to the unpublished source-only preview based on R10 commit `f041cdc8408e9eadc88b05fb95d884e25a8d1198`, prepared 2026-10-12. It does not claim native-game acceptance or a stable release. Development and real-device validation remain paused while the NS device is lent out.

The original 44-entry R10 source snapshot and the separate legacy 123-entry source manifest were read-only checked before copying; both had zero mismatches. Original R10 source/history and the legacy installation are preserved. The original R10 tests dated 2026-10-05 and the legacy 83-pass record are historical, not fresh results.

Fresh checks executed on the isolated preview with .NET SDK **10.0.102**:

| Check | Result |
| --- | --- |
| Foundation Release build without catalogs | Passed; 0 warnings, 0 errors |
| Desktop Release build without catalogs | Passed; 0 warnings, 0 errors; no window launched |
| `--self-test-history` | 8 groups, 0 failures |
| `--self-test-r9` | 7 groups, 0 failures |
| `--self-test-r5` | 12 groups, 0 failures |
| `--self-test-catalog` | 7 groups, 0 failures; only generated synthetic catalog claims |

The fresh total is **34 focused synthetic groups**, not the historical 83 legacy checks or the historical complete R10 resource/UI review. All synthetic file/export outputs, build artifacts, CLI home and package cache were directed to disposable preparation scratch outside this attachment directory. No private save was read/written, application window shown, original repository changed or game/device operated.

The local build commands used `dotnet build -c Release --artifacts-path <preparation-scratch>/artifacts --disable-build-servers -p:RestoreIgnoreFailedSources=true Foundation.csproj` and the corresponding Desktop project command. Each suite ran the compiled Foundation assembly with a separate synthetic working directory. Exact sanitized logs and command/version records are provided in the local review folder, not mixed into source or historical test reports. The initial CLI-home PATH-tools notice was non-fatal; no global PATH or dotnet tool installation was requested.

Catalog/resource-dependent `--self-test-r10` and `--self-test-providers`, UI smoke, console operations and real-save tests were deliberately not run. Other runners may depend on earlier reports or fixtures. This is focused source/software evidence, not a complete application, compatibility, security, authorship or legal review result.

The final source manifest/boundary check and read-only original-source/history preservation checks are recorded separately in the review. CI has been prepared with read-only permissions and pinned official actions; it has not run on GitHub because no repository was published.
