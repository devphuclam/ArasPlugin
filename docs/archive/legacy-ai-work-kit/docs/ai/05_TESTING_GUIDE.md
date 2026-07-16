# 05 — Testing Guide

## Baseline commands

From `ARAS-Plugin\IdeaCadConnector` on Windows:

```powershell
# Restore and build (Debug)
dotnet restore IdeaCadConnector.sln
dotnet build IdeaCadConnector.sln --configuration Debug --no-restore

# Build (Release)
dotnet build IdeaCadConnector.sln --configuration Release --no-restore

# Tests (build must have succeeded first)
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --no-build
```

If MSBuild is unavailable, use Visual Studio Developer PowerShell or install Visual Studio Build Tools with .NET Framework 4.8 targeting pack.

## Test levels

### Unit

No live Aras. Covers:

- policies;
- validation;
- hash/diff;
- manifest migration;
- conflict classification;
- ViewModel behavior using fakes.

### Integration

Uses dedicated Aras test database. Covers:

- AML/OAuth;
- Vault upload/download;
- Document version/file link;
- PDM Commit and Branch schema;
- permission and lock behavior.

### Manual/UAT

Uses IronCAD and real desktop UI. Covers:

- open/edit/read-only;
- checkout/check-in;
- Clone/Push/Pull;
- conflict choices;
- failure recovery.

## Test quality rules

- Avoid tests that only assert non-null or mock invocation count.
- Assert observable state/result/error code.
- Add negative tests for cancellation, network error, permission denial and partial failure.
- Do not hide a baseline failure by loosening assertions.
- Mark live-Aras tests explicitly and never run them against production by default.

## Evidence file

`Verify-AiTicket.ps1` writes evidence under `.ai-work/verification/`. Attach the relevant log to the PR or paste its summary.
