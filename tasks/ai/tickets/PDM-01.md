# PDM-01

## Scope

Experimental, feature-flagged IronCAD normalization/export. The original `.ics` is read-only; staging, package validation, source hashing, document close/reopen, and final activation happen before success.

The flag is `EnablePdmNormalizeExport=true`; default is disabled. It must not be enabled for production or original CAD data until disposable runtime evidence is complete.

## Safety contract

- Dependency discovery uses `ModelLinkPath`, `GetExternallyLinkedInfo`, and child traversal. Any external dependency fails closed with `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` in this pass.
- Occurrences map by deterministic paths such as `0`, `0/1`, and `0/1/2`.
- Source SHA-256 fingerprints are rechecked before publication.
- Manifest schema 2 separates definitions, occurrences, and BOM-v2 records.
- Staged source and exported staging root use verified `IZBaseApp.CloseFile(IZDoc)` before package move.

## Runtime blocker

IronCAD 2025 disposable internal-only round-trip evidence is still required. The feature remains experimental and disabled.

## Verification commands

```powershell
dotnet restore IdeaCadConnector.sln
dotnet build IdeaCadConnector.sln --configuration Debug --no-restore
dotnet build IdeaCadConnector.sln --configuration Release --no-restore
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --no-build
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Release --no-restore --no-build
.\scripts\ai\Check-AiScope.ps1
.\scripts\ai\Verify-AiTicket.ps1 -TicketId PDM-01
```

No `.ics`, raw proprietary diagnostics, credentials, or absolute local paths belong in the repository.
