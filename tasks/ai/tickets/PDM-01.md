# PDM-01

## Scope

Experimental, feature-flagged IronCAD normalization/export. The original `.ics` is read-only; staging, package validation, source hashing, document close/reopen, pending publication, final activation, and cleanup happen before success.

The flag is `EnablePdmNormalizeExport=true`; default is disabled. It must not be enabled for production or original CAD data until disposable runtime evidence is complete.

## Safety contract

- Dependency discovery uses `ModelLinkPath`, `GetExternallyLinkedInfo`, and child traversal. Any external dependency fails closed with `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` in this pass.
- Occurrences map by deterministic paths such as `0`, `0/1`, and `0/1/2`.
- Source SHA-256 fingerprints are rechecked before publication.
- Manifest schema 2 separates definitions, occurrences, and BOM-v2 records.
- Schema-v2 validation treats definitions, occurrences, BOM-v2, root occurrence, and root file as authoritative; it rejects missing, duplicate, escaping, and orphan `.ics` files without relying on `legacyItemsProjection`.
- Staged source, exported staging root, and pending root use verified `IZBaseApp.CloseFile(IZDoc)` before package moves.
- Publication is `staging -> .pending -> validated final`, with rollback of failed pending/final packages and idempotent cleanup ordering that closes tracked documents before directory deletion.
- Relative external links resolve against the verified package `cad` directory. External-reference traversal has active-cycle, depth, and node-count guards.
- Reopened staging, pending, and final packages compare root identity, occurrence paths, kinds, parent edges, PDM fields, counts, and maximum depth.
- The original source document is never assigned to a temporary-document slot and is never passed to `Save`, `SaveAs`, or `CloseFile` by this command.

## Runtime blocker

IronCAD 2025 disposable internal-only round-trip evidence is still required (`BLOCKED_ROUND_TRIP_VALIDATION`). Reliable source-document reactivation after a failure has not been implemented because no verified ICAPI activation method is available; the source remains open and unchanged.

Packages with any external source dependency remain unsupported and stop before staging with `BLOCKED_SOURCE_DEPENDENCY_ISOLATION`.

The feature remains experimental and disabled for original or production CAD data.

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
