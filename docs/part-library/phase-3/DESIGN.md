# Phase 3 Design

## Scope

Phase 3 makes the desktop connector deployable as an internal release candidate. Sprint 3.1 intentionally avoids runtime feature changes and focuses on a repeatable release package process.

## Release Package Model

The release zip contains one root folder:

```text
IdeaCadConnector-v0.3.0-rc1/
  app/
  aras/
    server-methods/
    README-Aras-Deployment.md
  docs/
  checksums/
  VERSION.txt
```

The `app/` folder is copied from the desktop Release output folder. This keeps runtime DLL selection aligned with MSBuild and avoids guessing transitive dependencies.

## Configuration Model (Sprint 3.2)

The desktop app now includes an environment configuration model for non-secret settings:

### Config File

```text
IdeaCadConnector.environment.json
```

### Lookup Order

1. `IDEA_CAD_CONNECTOR_ENV_CONFIG` environment variable
2. Next to executable (`app/IdeaCadConnector.environment.json`)
3. `%APPDATA%/IdeaCadConnector/IdeaCadConnector.environment.json`
4. Built-in defaults (no crash if missing)

### Supported Sections

| Section | Purpose | Wiring Status |
|---------|---------|---------------|
| `aras` | Base URL, database, Open-in-Aras URL | Not wired (prefill candidate for Sprint 3.3) |
| `local` | Vault cache directory, IronCAD path, auto-open | Not wired (candidate for Sprint 3.3) |
| `roles` | Manager/reviewer/contributor/read-only user aliases | Documented only (candidate for Sprint 3.3) |
| `diagnostics` | Log level, file logging, log directory | Not wired (requires logging infrastructure) |

### Secret Policy

No passwords, tokens, credentials, cookies, or sessions in config. Secret-like keys are detected at load time and emit warnings.

### Validation

- `schemaVersion` must be `1`
- Malformed JSON produces clear error with defaults fallback
- Empty file treated as missing
- Path expansion for `%LOCALAPPDATA%`, `%APPDATA%`, `%USERPROFILE%`

### Template

```text
docs/part-library/phase-3/templates/IdeaCadConnector.environment.template.json
```

Also included in release package as `docs/templates/IdeaCadConnector.environment.template.json`.

## Aras Dependency

The only required Aras method for this release candidate is:

```text
idea_GetPrimaryIronCadForPart
```

It is read-only and returns the best primary IronCAD CAD linked to a Part through `Part CAD`. The package includes the method source as deployment reference material.

## Packaging Script Design

`tools/release/package-release.ps1`:

- validates repository paths;
- builds the solution in the requested configuration;
- stages app output, Aras method source, and docs;
- writes `VERSION.txt`;
- writes `checksums/SHA256SUMS.txt`;
- creates `IdeaCadConnector-<Version>.zip`;
- never copies source `bin/obj`, `.vs`, test results, Vault/cache folders, or credentials.

## Installation Hardening (Sprint 3.3)

Sprint 3.3 adds practical handoff docs and a validation script:

- **Installation hardening guide**: extraction location, run steps, config setup, verify, rollback
- **Machine readiness guide**: Windows/.NET 4.8, network, roles, Aras permissions
- **Troubleshooting guide**: 14 issues with symptom/cause/check/action/severity (P0/P1/P2)
- **Internal UAT result template**: 25-area fillable table with PASS/FAIL/BLOCKED/N/A
- **IT handoff guide**: what to send/not send, Aras/machine prep, smoke test, escalation
- **Package validation script**: `tools/release/validate-release-package.ps1` — validates structure, files, config, forbidden content, secrets; returns exit code
- **Packaging**: all Sprint 3.3 docs included in release zip under `docs/`; validation script under `tools/`

## Production Release Readiness (Sprint 3.4)

Sprint 3.4 adds final release-readiness documentation and a verification script. No source code changes.

- **Production readiness doc**: summarizes all prerequisites, package contents, security policy, escalation path, and final decision form
- **Go/No-Go checklist**: 10-section checklist covering build/test, package, Aras, roles, machine, security, rollback, limitations, and sign-offs
- **Release sign-off template**: fillable form for tracking sign-off decisions
- **Release manifest**: authoritative list of all package contents, exclusions, and sprint acceptance history
- **Known limitations doc**: centralized list of accepted limitations
- **Phase 3 closeout plan**: remaining steps before marking Phase 3 COMPLETE
- **Release verification script**: `tools/release/verify-release-readiness.ps1` — runs build, test, package, validate in sequence
- **Packaging**: Sprint 3.4 docs included in release zip under `docs/`; verification script under `tools/`
- **Validation**: Sprint 3.4 docs and scripts verified by updated validation script

## Risks

- The package is a zip, not an installer; users need manual extraction guidance.
- Environment configuration is still partly manual.
- IronCAD open depends on a local IronCAD installation and path association.
- Vault download depends on live Aras File/Vault permissions.
