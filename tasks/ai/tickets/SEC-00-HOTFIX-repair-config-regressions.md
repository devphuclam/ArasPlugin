# SEC-00-HOTFIX — Repair configuration regressions and unsafe tests

Status: In Progress

## Scope

Repair the regressions introduced by SEC-00 while preserving the security baseline. This ticket does not modify Aras schema, server methods, Document Vault behavior, DOC-03, Pull, Branch, or PDM Commit work.

## Root causes

- Configuration precedence tests wrote beside the executable and under real AppData, then removed shared paths during cleanup.
- PDM open flows constructed `IronCadExternalAdapter` without the configured executable path.
- BaseUri values were accepted without HTTP/HTTPS validation or canonical trailing-slash normalization.
- The HTTP authentication path hardcoded OAuth client ID and scope.
- An explicit but invalid `IDEA_CAD_CONNECTOR_ENV_CONFIG` value silently fell through to another environment.
- Current tracked documentation and tests contained infrastructure-specific values and role identifiers.
- SEC-00 completion evidence was recorded without an independent fresh verification gate.

## Behavior after hotfix

- Loader tests use only unique temporary candidate roots and never delete shared AppData/output paths.
- An explicit environment-config path is authoritative and returns a controlled error when missing, a directory, unreadable, or malformed.
- BaseUri accepts only absolute HTTP/HTTPS values and is normalized to exactly one trailing slash for file config and login overrides.
- HTTP OAuth requests use the configured client ID and scope; missing values fail validation before network access.
- PDM adapter creation reads the configured IronCAD path from session/options and fails clearly when absent.
- Current tracked values are synthetic/placeholders. Historical commits remain unchanged and require a separately approved history-cleanup operation.

## Verification record

This section is completed only from fresh commands on the hotfix branch. It must include exact Debug/Release build and test counts, `Check-AiScope`, `Verify-AiTicket`, security scan results, and commit SHAs. No live Aras connection or production credentials are used.

## Acceptance criteria

- [ ] HOTFIX-01 through HOTFIX-05 regression tests pass.
- [ ] Current-tree security scans have no private IP URL, exposed database/Vault value, machine-specific IronCAD path, or internal role code.
- [ ] Debug and Release solution builds pass.
- [ ] Debug and Release test suites pass with exact counts recorded.
- [ ] `Check-AiScope` passes.
- [ ] `Verify-AiTicket -TicketId SEC-00-HOTFIX` passes, or any independent script defect is documented exactly.
- [ ] Draft PR targets `main`; it is not merged.
