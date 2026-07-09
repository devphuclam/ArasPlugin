# Release Packaging

Sprint 3.2 Config Package UAT accepted. Template `IdeaCadConnector.environment.template.json` included at `docs/templates/`. Active config rejected by script. No secrets in template.

Sprint 3.3 Internal Installation Package UAT accepted on 2026-07-09. Package `IdeaCadConnector-v0.3.0-rc1.zip` includes 5 new hardening docs, package validation script, and all prior content. See [ACCEPTANCE.md](ACCEPTANCE.md).

Sprint 3.4 adds production readiness docs (PRODUCTION-READINESS, GO-NO-GO-CHECKLIST, RELEASE-SIGNOFF-TEMPLATE, RELEASE-MANIFEST, KNOWN-LIMITATIONS, PHASE-3-CLOSEOUT-PLAN) and a release verification script (verify-release-readiness.ps1). See [ACCEPTANCE.md](ACCEPTANCE.md).

## Script

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\release\package-release.ps1 -Version v0.3.0-rc1 -Configuration Release
```

## Output

Default output folder:

```text
artifacts/release/
```

Default zip:

```text
artifacts/release/IdeaCadConnector-v0.3.0-rc1.zip
```

## Zip Structure

```text
IdeaCadConnector-v0.3.0-rc1/
  app/
    IdeaCadConnector.Desktop.exe
    required dll files
    required runtime/config files from Release output
  aras/
    server-methods/
      idea_GetPrimaryIronCadForPart.cs
    README-Aras-Deployment.md
  docs/
    INSTALL.md
    CONFIGURATION.md
    UAT-CHECKLIST.md
    ROLLBACK.md
    RELEASE-NOTES.md
    INSTALLATION-HARDENING.md
    MACHINE-READINESS.md
    TROUBLESHOOTING.md
    INTERNAL-UAT-RESULT-TEMPLATE.md
    IT-HANDOFF.md
    PRODUCTION-READINESS.md
    GO-NO-GO-CHECKLIST.md
    RELEASE-SIGNOFF-TEMPLATE.md
    RELEASE-MANIFEST-v0.3.0-rc1.md
    KNOWN-LIMITATIONS.md
    PHASE-3-CLOSEOUT-PLAN.md
    templates/
      IdeaCadConnector.environment.template.json
  tools/
    validate-release-package.ps1
    verify-release-readiness.ps1
  checksums/
    SHA256SUMS.txt
  VERSION.txt
```

## Excluded Content

The package must not include:

- secrets;
- local connection files;
- cached CAD or Vault files;
- source `bin/obj` folders;
- `.vs`;
- `TestResults`;
- personal developer configuration;
- generated test artifacts;
- active `IdeaCadConnector.environment.json` (user-specific config with potential secrets);
- any file named `IdeaCadConnector.environment.json` outside the `docs/templates/` folder.

## Active Config Validation

The packaging script validates that no active `IdeaCadConnector.environment.json` is present in the repository outside the docs template folder before building the release zip. If an active config is found, the script exits with an error to prevent accidental secret inclusion.

## Version File

`VERSION.txt` records:

- version;
- git commit SHA;
- build timestamp in UTC;
- configuration.

## Checksums

`checksums/SHA256SUMS.txt` contains SHA256 checksums for package files. The zip file itself is not included in the internal checksum file because the checksum file is written before compression.
