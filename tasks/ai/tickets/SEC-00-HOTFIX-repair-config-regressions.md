# SEC-00-HOTFIX — Repair configuration regressions and unsafe tests

Status: Completed — 2026-07-13

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

Fresh verification on the hotfix branch used no live Aras connection or production credentials:

- Debug solution build: succeeded, 0 errors, 12 existing IronCAD post-build `Access is denied` warnings.
- Release solution build: succeeded, 0 errors, 12 existing IronCAD post-build `Access is denied` warnings.
- Debug tests: 482 passed, 0 failed, 0 skipped.
- Release tests: 482 passed, 0 failed, 0 skipped.
- `Check-AiScope.ps1`: passed with a clean working tree.
- `Verify-AiTicket.ps1 -TicketId SEC-00-HOTFIX`: passed; build exit 0 and test exit 0; evidence under `.ai-work/verification/SEC-00-HOTFIX-20260713-113709`.
- Current-tree scans: no private-IP URL, exposed infrastructure database/Vault value, machine-specific IronCAD install path, or original role code.
- `dotnet restore IdeaCadConnector.sln`: independently failed in this environment during NuGet/MSBuild restore-graph traversal with exit 1 and no reported project error; project restore for Core and Aras passed, while the test-project restore reproduced the same restore-graph failure. Builds/tests were run with `--no-restore` against the existing package cache.

## Acceptance criteria

- [x] HOTFIX-01 through HOTFIX-05 regression tests pass.
- [x] Current-tree security scans have no private IP URL, exposed database/Vault value, machine-specific IronCAD path, or internal role code.
- [x] Debug and Release solution builds pass.
- [x] Debug and Release test suites pass with exact counts recorded.
- [x] `Check-AiScope` passes.
- [x] `Verify-AiTicket -TicketId SEC-00-HOTFIX` passes.
- [ ] Draft PR targets `main`; it is not merged.
