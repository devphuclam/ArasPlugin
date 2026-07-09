# Release Manifest — v0.3.0-rc1

## Version

```
v0.3.0-rc1
```

## Package Name

```
IdeaCadConnector-v0.3.0-rc1.zip
```

## Source Commit

```
dc4fe6c17d041a6618662187c209532e7fd0be0a
```

## Package Timestamp

Generated at build time. Recorded in `VERSION.txt` as `build_timestamp_utc`.

## Checksums

```
checksums/SHA256SUMS.txt
```

## App Executable

```
app/IdeaCadConnector.Desktop.exe
```

## Included Aras Method

```
aras/server-methods/idea_GetPrimaryIronCadForPart.cs
```

## Included Docs

- INSTALL.md
- CONFIGURATION.md
- UAT-CHECKLIST.md
- ROLLBACK.md
- RELEASE-NOTES.md
- INSTALLATION-HARDENING.md
- MACHINE-READINESS.md
- TROUBLESHOOTING.md
- INTERNAL-UAT-RESULT-TEMPLATE.md
- IT-HANDOFF.md
- PRODUCTION-READINESS.md
- GO-NO-GO-CHECKLIST.md
- RELEASE-SIGNOFF-TEMPLATE.md
- RELEASE-MANIFEST-v0.3.0-rc1.md
- KNOWN-LIMITATIONS.md
- PHASE-3-CLOSEOUT-PLAN.md
- templates/IdeaCadConnector.environment.template.json

## Included Scripts

- tools/validate-release-package.ps1
- tools/verify-release-readiness.ps1

## Excluded Files

- Active `IdeaCadConnector.environment.json`
- Secrets (passwords, tokens, credentials)
- Build/test artifacts (bin/, obj/, TestResults/)
- .vs/ solution cache
- local developer config files
- generated zip files

## Build/Test Baseline

| Check | Result |
|---|---|
| Debug build | 0 warnings, 0 errors |
| Release build | 0 warnings, 0 errors |
| Debug tests | 419/419 pass |
| Release tests | 419/419 pass |
| Package script | PASS |
| Validation script | 30/30 PASS |

## Sprint Acceptance Summary

| Sprint | Scope | Status |
|---|---|---|
| 3.1 | Release Packaging Baseline | `PACKAGE_UAT_ACCEPTED` |
| 3.2 | Environment Configuration Hardening | `CONFIG_PACKAGE_UAT_ACCEPTED` |
| 3.3 | Internal Installation/UAT Hardening | `INTERNAL_INSTALLATION_PACKAGE_UAT_ACCEPTED` |
| 3.4 | Production Release Readiness | `PRODUCTION_READINESS_UAT_ACCEPTED` |

## Phase 3 Final State

**Phase 3 — Deployment and Production Hardening:** `COMPLETE`

**Closeout commit:** `dc4fe6c17d041a6618662187c209532e7fd0be0a`

**Recommended tag:** `v0.3.0-rc1` (not created in this commit)

## Known Limitations

See [KNOWN-LIMITATIONS.md](KNOWN-LIMITATIONS.md).
