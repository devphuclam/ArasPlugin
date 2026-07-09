# Part Library Phase 3 - Deployment and Production Hardening

**State:** `COMPLETE`

## Objective

Phase 3 prepares the IdeaCadConnector desktop app for repeatable internal release candidate delivery.

Target release candidate:

```text
IdeaCadConnector v0.3.0-rc1
```

Sprint 3.1 creates the release packaging baseline: build, package, checksum, deployment notes, environment guidance, Aras prerequisite notes, rollback guidance, and UAT checklist.

## Non-Goals

- no new Part Library features;
- no new Aras server methods;
- no live Aras changes;
- no MSI, ClickOnce, or auto-update system;
- no database schema change;
- no production credentials or local connection files in the repository;
- no Phase 4 work.

## Baseline

Sprint 3.1 baseline commit:

```text
35494964519e014ee60e573a3db718770668ba8c
```

Phase 2 is `COMPLETE` (all sprints accepted, final live App UAT passed). Phase 3 is now `COMPLETE` (all 4 sprints accepted).

## Current State

- Desktop target framework: `net48`
- Desktop executable: `IdeaCadConnector.Desktop.exe`
- Desktop output folder used for packaging: `src/IdeaCadConnector.Desktop/bin/<Configuration>/net48`
- Desktop config files currently copied to output: `pdm-naming-policy.json`
- `src/IdeaCadConnector.Desktop/App.config` does not exist.
- Required live Aras method: `idea_GetPrimaryIronCadForPart`
- Environment config model added: `IdeaCadConnector.environment.json` with `EnvironmentConfigurationLoader` in `IdeaCadConnector.Core.Configuration`
- Config file name: `IdeaCadConnector.environment.json`
- Lookup order: (1) `IDEA_CAD_CONNECTOR_ENV_CONFIG` env var, (2) next to executable, (3) `%APPDATA%/IdeaCadConnector/`, (4) built-in defaults
- Template: `docs/templates/IdeaCadConnector.environment.template.json` included in release package
- Active config excluded from package by script validation

## Sprint 3.4 Workstreams

| ID | Workstream | Status |
|---|---|---|
| `WS3.4-A` | Production readiness doc | `ACCEPTED` |
| `WS3.4-B` | Go/No-Go checklist | `ACCEPTED` |
| `WS3.4-C` | Release sign-off template | `ACCEPTED` |
| `WS3.4-D` | Release manifest | `ACCEPTED` |
| `WS3.4-E` | Known limitations doc | `ACCEPTED` |
| `WS3.4-F` | Phase 3 closeout plan | `ACCEPTED` |
| `WS3.4-G` | Release verification script | `ACCEPTED` |
| `WS3.4-H` | Packaging: include Sprint 3.4 docs/scripts | `ACCEPTED` |
| `WS3.4-I` | Validation: check Sprint 3.4 docs/scripts | `ACCEPTED` |

## Sprint 3.3 Workstreams

| ID | Workstream | Status |
|---|---|---|
| `WS3.3-A` | Installation hardening guide | `ACCEPTED` |
| `WS3.3-B` | Machine readiness guide | `ACCEPTED` |
| `WS3.3-C` | Troubleshooting guide | `ACCEPTED` |
| `WS3.3-D` | Internal UAT result template | `ACCEPTED` |
| `WS3.3-E` | IT handoff guide | `ACCEPTED` |
| `WS3.3-F` | Package validation script | `ACCEPTED` |
| `WS3.3-G` | Packaging: include new docs/scripts | `ACCEPTED` |

## Sprint 3.2 Workstreams

| ID | Workstream | Status |
|---|---|---|
| `WS3.2-A` | Environment config model and loader | `IMPLEMENTED_LOCALLY` |
| `WS3.2-B` | Config validation (schema version, secret detection, malformed JSON) | `IMPLEMENTED_LOCALLY` |
| `WS3.2-C` | Config template (Desktop + docs) | `IMPLEMENTED_LOCALLY` |
| `WS3.2-D` | Packaging: template inclusion + active config rejection | `IMPLEMENTED_LOCALLY` |
| `WS3.2-E` | Config tests (16 new) | `IMPLEMENTED_LOCALLY` |
| `WS3.2-F` | Docs: environment config reference, per-machine setup, lookup order | `IMPLEMENTED_LOCALLY` |

## Workstreams (Sprint 3.1)
| `WS3.1-A` | Release zip structure | `ACCEPTED` |
| `WS3.1-B` | Repeatable packaging script | `ACCEPTED` |
| `WS3.1-C` | Install/config/UAT/rollback docs | `ACCEPTED` |
| `WS3.1-D` | Aras deployment bundle guidance | `ACCEPTED` |
| `WS3.1-E` | Build/test/package validation | `ACCEPTED` |

## Sprint Plan

| Sprint | Scope | State |
|---|---|---|---|
| `3.1` | Release Packaging Baseline | `PACKAGE_UAT_ACCEPTED` |
| `3.2` | Environment Configuration Hardening | `CONFIG_PACKAGE_UAT_ACCEPTED` |
| `3.3` | Internal Installation/UAT Hardening | `INTERNAL_INSTALLATION_PACKAGE_UAT_ACCEPTED` |
| `3.4` | Production Release Readiness | `PRODUCTION_READINESS_UAT_ACCEPTED` |

## Sprint 3.1 Package UAT Result

**Package tested:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Package UAT accepted** on 2026-07-08.

| Area | Result | Evidence |
|---|---|---|
| Extract zip | PASS | Clean folder extraction succeeded |
| VERSION.txt | PASS | File exists in package root |
| SHA256SUMS.txt | PASS | File exists under checksums |
| App launch | PASS | Desktop app launched from clean extracted folder |
| Login Aras | PASS | Login completed successfully |
| Part Library load | PASS | Part Library opened and loaded |
| Aras method included | PASS | `idea_GetPrimaryIronCadForPart.cs` included in package |
| Docs readable | PASS | INSTALL/CONFIGURATION/UAT/ROLLBACK docs readable |
| Missing DLL check | PASS | No missing DLL error |
| Secret/artifact check | PASS | No secret/artifact issue found |

## Sprint 3.1 Acceptance Gates (all met)

- [x] release packaging docs exist;
- [x] packaging script creates a zip for `v0.3.0-rc1`;
- [x] `VERSION.txt` and `SHA256SUMS.txt` are generated;
- [x] required Aras method source is included;
- [x] install, configuration, UAT, rollback, and release notes are included;
- [x] Debug and Release builds pass;
- [x] Debug and Release tests pass;
- [x] no secrets or generated release artifacts are committed.

## Sprint 3.2 Acceptance Gates (all met)

- [x] environment config model exists;
- [x] safe config loader exists;
- [x] missing config does not crash;
- [x] template exists;
- [x] package includes template only;
- [x] no secrets committed;
- [x] official role defaults remain correct;
- [x] tests pass;
- [x] package script passes;
- [x] docs updated;
- [x] Phase 3 remains `IN_PROGRESS`;

## Sprint 3.3 Internal Installation Package UAT

**Package tested:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Commit tested:** `00d4b70454d7daf438d44385dde3dcebf72fbd0b`

**Internal Installation Package UAT accepted** on 2026-07-09.

| Area | Result | Evidence |
|---|---|---|
| Package script rerun | PASS | `package-release.ps1` completed |
| Validation on zip | PASS | `validate-release-package.ps1` zip: 23/23 PASS |
| Clean extraction | PASS | zip extracted to clean folder |
| Validation on extracted folder | PASS | `validate-release-package.ps1` extracted: 23/23 PASS |
| New docs included | PASS | Sprint 3.3 docs present in package |
| Validation script included | PASS | `tools/validate-release-package.ps1` present |
| Machine readiness doc | PASS | usable by internal tester |
| Troubleshooting doc | PASS | usable by internal tester |
| Internal UAT result template | PASS | usable by internal tester |
| IT handoff doc | PASS | usable by IT/internal handoff |
| Active config excluded | PASS | no active `IdeaCadConnector.environment.json` packaged |
| Secret scan | PASS | no secrets found |
| App launch | PASS | app launched from extracted folder |
| Login Aras | PASS | login completed |
| Part Library load | PASS | Part Library loaded |
| Known issues | PASS | none reported |

## Sprint 3.3 Acceptance Gates (all met)

- [x] installation hardening guide exists;
- [x] machine readiness guide exists;
- [x] troubleshooting guide exists;
- [x] IT handoff guide exists;
- [x] internal UAT result template exists;
- [x] package validation script exists;
- [x] package script includes Sprint 3.3 docs/scripts;
- [x] package validation script passes against generated zip (23/23 checks);
- [x] Debug build passes;
- [x] Release build passes;
- [x] Debug tests pass;
- [x] Release tests pass;
- [x] no source feature changes;
- [x] no secrets committed;
- [x] Phase 3 remains `IN_PROGRESS`;
- [x] Sprint 3.4 not started.

## Phase 3 Closeout

Phase 3 — Deployment and Production Hardening is now `COMPLETE`.

All 4 sprints accepted:

| Sprint | Scope | Status |
|---|---|---|
| 3.1 | Release Packaging Baseline | `PACKAGE_UAT_ACCEPTED` |
| 3.2 | Environment Configuration Hardening | `CONFIG_PACKAGE_UAT_ACCEPTED` |
| 3.3 | Internal Installation/UAT Hardening | `INTERNAL_INSTALLATION_PACKAGE_UAT_ACCEPTED` |
| 3.4 | Production Release Readiness | `PRODUCTION_READINESS_UAT_ACCEPTED` |

**Final package:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Final build/test:** Debug 0w/0e, Release 0w/0e, 419/419 tests pass.

**Known limitations:** Documented in [KNOWN-LIMITATIONS.md](KNOWN-LIMITATIONS.md).

## Current Owner

Codex local implementation, with project-owner UAT and release approval required before production use.

## Rollback Considerations

Release candidate rollback is file-based:

- stop the desktop app;
- restore the previous release folder or previous zip extraction;
- leave user workspaces, Vault cache, and Aras data untouched;
- remove only the failed release candidate files if required.

Aras rollback is limited to disabling/removing the read-only method if it was newly deployed for a target environment. Sprint 3.1 does not mutate live Aras.

## Supporting Documents

- [Design](DESIGN.md)
- [Deployment](DEPLOYMENT.md)
- [Acceptance](ACCEPTANCE.md)
- [Release Packaging](RELEASE-PACKAGING.md)
- [Environment Configuration](ENVIRONMENT-CONFIGURATION.md)
- [Rollback](ROLLBACK.md)
- [UAT Checklist](UAT-CHECKLIST.md)
- [Release Notes v0.3.0-rc1](RELEASE-NOTES-v0.3.0-rc1.md)
- [Production Readiness](PRODUCTION-READINESS.md)
- [Go/No-Go Checklist](GO-NO-GO-CHECKLIST.md)
- [Release Sign-Off Template](RELEASE-SIGNOFF-TEMPLATE.md)
- [Release Manifest v0.3.0-rc1](RELEASE-MANIFEST-v0.3.0-rc1.md)
- [Known Limitations](KNOWN-LIMITATIONS.md)
- [Phase 3 Closeout Plan](PHASE-3-CLOSEOUT-PLAN.md)
