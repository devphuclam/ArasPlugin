# Production Readiness

## Package Identity

```text
IdeaCadConnector-v0.3.0-rc1.zip
```

## Required Build/Test Baseline

| Check | Result |
|---|---|
| Debug build | 0 warnings, 0 errors |
| Release build | 0 warnings, 0 errors |
| Debug tests | 419/419 pass |
| Release tests | 419/419 pass |

## Package Validation

`validate-release-package.ps1` returns exit code 0 (23/23 checks PASS).

## Aras Prerequisites

- Server method `idea_GetPrimaryIronCadForPart` deployed to target database.
- User has Execute Method permission on `idea_GetPrimaryIronCadForPart`.
- User has Get permission on `Part`, `CAD`, `Part CAD`, `File`.
- User has Get permission on `idea_PartLibrary`, `idea_PartLibraryEntry`.
- Role identity matches one of: TPTKC, TNTKC, NVTKC, NVLCR, PM, Khách hàng.

## Machine Prerequisites

| Requirement | Details |
|---|---|
| Windows | Windows 10 or Windows Server 2016+ (x64) |
| .NET Framework | 4.8 Runtime installed |
| Network | Access to target Aras Innovator server URL |
| Browser | Default browser for Open-in-Aras links |
| IronCAD | Installed if Open in IronCAD will be tested |
| Disk space | ~200 MB for app + vault cache |

## Release Package Contents

```text
IdeaCadConnector-v0.3.0-rc1/
  app/
    IdeaCadConnector.Desktop.exe + required DLLs
  aras/
    server-methods/idea_GetPrimaryIronCadForPart.cs
    README-Aras-Deployment.md
  docs/
    INSTALL.md, CONFIGURATION.md, UAT-CHECKLIST.md
    ROLLBACK.md, RELEASE-NOTES.md
    INSTALLATION-HARDENING.md, MACHINE-READINESS.md
    TROUBLESHOOTING.md, INTERNAL-UAT-RESULT-TEMPLATE.md
    IT-HANDOFF.md
    PRODUCTION-READINESS.md, GO-NO-GO-CHECKLIST.md
    RELEASE-SIGNOFF-TEMPLATE.md, RELEASE-MANIFEST-v0.3.0-rc1.md
    KNOWN-LIMITATIONS.md, PHASE-3-CLOSEOUT-PLAN.md
    templates/IdeaCadConnector.environment.template.json
  tools/
    validate-release-package.ps1
    verify-release-readiness.ps1
  checksums/SHA256SUMS.txt
  VERSION.txt
```

## No Secrets Policy

- No passwords, tokens, credentials, cookies, or session keys in config file.
- Secret-like keys are detected and warned at load time.
- Active config is never included in release package.
- Use Aras login dialog for credentials.

## Rollback Procedure

See [ROLLBACK.md](ROLLBACK.md) for file-based rollback steps.

## Support / Escalation Path

| Issue | Contact |
|---|---|
| App crash or bug | Developer via issue tracker |
| Missing DLL / packaging | Developer |
| Aras permission / method | Aras administrator |
| Network / machine | IT support |
| UAT / readiness decision | Project owner |

## Final Decision

- [ ] **GO** — All checks pass. Ready for wider UAT or production.
- [ ] **NO-GO** — Blocking issue found. List blocker below.
- [ ] **GO WITH ACCEPTED LIMITATIONS** — Non-blocking issues documented and accepted.
