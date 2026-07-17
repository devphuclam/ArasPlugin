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

## IronCAD add-in registration checklist

When the `IDEA PDM` ribbon or `Chuẩn hóa & Xuất PDM` button disappears, do not change
the add-in source first. IronCAD 2025 reports internal version `27.0` and the host
is x64; the add-in must therefore be built x64 and registered against the
`IRONCAD 27.0` application key.

The COM registration must be generated from the built `IdeaCadConnector.IronCAD.dll`
with .NET `RegAsm`, including the ICAPI `Record/{GUID}` entries. A hand-written
CLSID/ProgID-only registration is insufficient: IronCAD may list the add-in but
silently uncheck it when loading the ICAPI types. For a non-admin user, import the
generated registration into `HKCU\Software\Classes` and keep the IronCAD application
entry under `HKCU\Software\IronCAD\IRONCAD 27.0\Applications\IdeaCadConnector`.

After registration, open IronCAD's `Add-Ins > Add-in Applications`, select
`IdeaCadConnector` once, confirm the dialog, and restart IronCAD with a Scene open.
Verify that the add-in is checked and that the IDEA PDM ribbon is visible. COM
activation from an external PowerShell process is not sufficient evidence that
IronCAD has loaded the add-in.

Assert observable state, results, and error codes. Include negative paths for cancellation, network errors, permission denial, and partial failure. Do not run live integration tests against production by default.

Source reference: archived `docs/archive/legacy-ai-work-kit/docs/ai/05_TESTING_GUIDE.md`.
