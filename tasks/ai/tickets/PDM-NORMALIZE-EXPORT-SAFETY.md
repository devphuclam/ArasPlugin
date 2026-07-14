# PDM-NORMALIZE-EXPORT-SAFETY

## Scope

Experimental, feature-flagged IronCAD normalization/export. The command reads the original `.ics`, discovers external links, creates an isolated staging copy, applies changes only to staging, exports and validates a package, then publishes it.

The flag is `EnablePdmNormalizeExport=true`; the default is disabled. It must not be enabled for production or original CAD data until runtime evidence is complete.

## Safety contract

- The original source package is never a write target.
- Source SHA-256 fingerprints are captured before staging and rechecked before publication.
- Dependency discovery uses the verified ICAPI members `IZSceneElement.ModelLinkPath`, `IZPart.GetExternallyLinkedInfo`, `IZAssembly.GetExternallyLinkedInfo`, and child traversal. External dependencies currently fail closed with `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` until relinking is runtime verified.
- Occurrences map by deterministic paths such as `0`, `0/1`, and `0/1/2`; names and COM object identity are not mapping keys.
- Manifest schema 2 separates definitions and occurrences. Without runtime definition identity, each occurrence remains separate and BOM quantity is `IdentityUnavailable`.
- Package staging is validated and the exported root is reopened before the final directory move.

## Runtime blocker

IronCAD 2025 disposable-copy scenarios still need observed evidence for complete dependency copy/relink and exported-root external-reference round-trip. Until Scenario A and Scenario B are recorded with sanitized evidence, the feature remains experimental and disabled.

## Verification commands

Run from the repository root:

```powershell
dotnet restore IdeaCadConnector.sln
dotnet build IdeaCadConnector.sln --configuration Debug --no-restore
dotnet build IdeaCadConnector.sln --configuration Release --no-restore
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --no-build
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Release --no-restore --no-build
.\scripts\ai\Check-AiScope.ps1
.\scripts\ai\Verify-AiTicket.ps1 -TicketId PDM-NORMALIZE-EXPORT-SAFETY
```

No `.ics`, raw proprietary diagnostics, credentials, or absolute local paths belong in the repository.
