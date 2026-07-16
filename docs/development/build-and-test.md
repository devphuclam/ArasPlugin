# Build and Test

## Baseline commands

Run from the `ArasPlugin/` repository root:

```powershell
dotnet build IdeaCadConnector.sln
dotnet test IdeaCadConnector.sln
```

The expected result must be taken from the latest approved baseline evidence. Do not report pass when a command was not run; distinguish environment/dependency failures from regressions.

## Test levels

- Unit tests cover policies, validation, hashing/diff, manifest behavior, conflict classification, and ViewModel behavior using fakes.
- Integration tests use a dedicated Aras test environment for remote contracts, Vault, permissions, locks, and version behavior.
- Manual/UAT covers IronCAD and desktop workflows such as open/edit/read-only, checkout/check-in, Clone/Push/Pull, conflicts, and recovery.

Assert observable state, results, and error codes. Include negative paths for cancellation, network errors, permission denial, and partial failure. Do not run live integration tests against production by default.

Source reference: `docs/ai/05_TESTING_GUIDE.md`.
