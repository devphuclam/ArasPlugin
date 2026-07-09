# Phase 3 Deployment

## Release Candidate

```text
IdeaCadConnector v0.3.0-rc1
```

## Install Package Contents

The release zip contains:

- desktop app binaries under `app/`;
- required Aras method source under `aras/server-methods/`;
- Aras deployment notes under `aras/README-Aras-Deployment.md`;
- install, configuration, UAT, rollback, and release note docs under `docs/`;
- package checksums under `checksums/SHA256SUMS.txt`;
- package metadata under `VERSION.txt`.

## Internal Install Steps

1. Extract the zip to an internal user-writable folder.
2. Open `docs/CONFIGURATION.md` and confirm the target Aras base URL, database, login mode, and local IronCAD availability.
3. Confirm `aras/server-methods/idea_GetPrimaryIronCadForPart.cs` is deployed to the target Aras database.
4. Run `app/IdeaCadConnector.Desktop.exe`.
5. Sign in to Aras.
6. Run the UAT checklist in `docs/UAT-CHECKLIST.md`.

## Aras Prerequisites

Required method:

```text
idea_GetPrimaryIronCadForPart
```

Method type: C#

Behavior: read-only Part-to-primary-IronCAD-CAD lookup.

Input:

```text
part_id
```

Output:

```text
id,item_number,name,classification,authoring_tool,generation,state,locked_by_id,native_file
```

Required permissions:

- Execute Method;
- Get Part;
- Get CAD;
- Get Part CAD relationship;
- Get File/native_file metadata and Vault file where download is tested.

The method must not add, edit, promote, create relationships, upload files, or mutate live data.

## Organization Role Identities

| ID | Role |
|---|---|
| `TPTKC` | Truong phong thiet ke co |
| `TNTKC` | Truong nhom thiet ke co |
| `NVTKC` | Nhan vien thiet ke co |
| `NVLCR` | Nhan vien lap rap co |
| `PM` | Quan ly du an |
| `Customer` | Khach hang |

## Sprint 3.2 Config Package UAT

Sprint 3.2 Config Package UAT accepted. Template `IdeaCadConnector.environment.template.json` included in package at `docs/templates/`. Active config excluded by script validation. No secrets in template. Role defaults correct. App launches without config and does not crash with config.

## Sprint 3.4 Production Release Readiness

Sprint 3.4 adds production readiness docs, checklists, and a release verification script. The release package now includes:

- `docs/PRODUCTION-READINESS.md` — package identity, prerequisites, package contents, security policy, escalation, decision form
- `docs/GO-NO-GO-CHECKLIST.md` — 10-section go/no-go decision checklist
- `docs/RELEASE-SIGNOFF-TEMPLATE.md` — fillable release sign-off form
- `docs/RELEASE-MANIFEST-v0.3.0-rc1.md` — authoritative release manifest
- `docs/KNOWN-LIMITATIONS.md` — centralized known limitations and accepted risks
- `docs/PHASE-3-CLOSEOUT-PLAN.md` — remaining steps before Phase 3 COMPLETE
- `tools/verify-release-readiness.ps1` — release readiness verification script

Sprint 3.4 Production Readiness UAT accepted. Phase 3 is now `COMPLETE`.

## Sprint 3.3 Internal Installation Package UAT

**Sprint 3.3 Internal Installation Package UAT accepted** on 2026-07-09.

Package `IdeaCadConnector-v0.3.0-rc1.zip` validated against zip and extracted folder (23/23 checks PASS). App launched, Aras login passed, Part Library loaded. All new docs included and usable. No known issues.

Sprint 3.3 adds installation hardening docs and a package validation script. The release package now includes:

- `docs/INSTALLATION-HARDENING.md` — extraction, run, verify, rollback
- `docs/MACHINE-READINESS.md` — machine/network/permissions prerequisites
- `docs/TROUBLESHOOTING.md` — 14 common issues with severity classification
- `docs/INTERNAL-UAT-RESULT-TEMPLATE.md` — fillable 25-area test result form
- `docs/IT-HANDOFF.md` — what to send/not send, prep steps, escalation
- `tools/validate-release-package.ps1` — package integrity validation script

## Environment Configuration (Sprint 3.2)

The release package includes an environment config template at:

```text
docs/templates/IdeaCadConnector.environment.template.json
```

Users should copy this template to `IdeaCadConnector.environment.json` and place it in one of the lookup locations:

1. Set `IDEA_CAD_CONNECTOR_ENV_CONFIG` environment variable to the config path.
2. Place next to the executable in the `app/` folder.
3. Place in `%APPDATA%/IdeaCadConnector/` folder.

Edit non-secret values only. Do not add passwords or tokens.

The packaging script validates that no active `IdeaCadConnector.environment.json` is included in the release zip.

## Rollback

Rollback is file-based for Sprint 3.1 and Sprint 3.2. See [ROLLBACK.md](ROLLBACK.md).
