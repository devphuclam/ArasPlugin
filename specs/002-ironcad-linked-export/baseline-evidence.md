# Baseline Evidence: IronCAD Linked Normalized Export

**Branch**: `002-ironcad-linked-export`
**Date**: 2026-07-16
**Pre-existing dirty-tree**: Yes — 5 modified, 2 untracked (unrelated to this feature)

## Build

```
dotnet build IdeaCadConnector.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:09.76
```

## Tests

```
dotnet test IdeaCadConnector.sln
Passed!  - Failed:     0, Passed:   645, Skipped:     0, Total:   645, Duration: 5 s
```

## Git Status

```
 M CONTEXT.md
 M docs/development/build-and-test.md
 M src/IdeaCadConnector.IronCAD/IdeaCadConnector.IronCAD.csproj
 M src/IdeaCadConnector.IronCAD/IdeaCadConnector.IronCAD.reg
 M src/IdeaCadConnector.IronCAD/IronCadAddin.cs
?? .specify/feature.json
?? specs/002-ironcad-linked-export/
```

## Final Verification — 2026-07-17

This verification was run after the native externalization implementation and Spec Kit documentation updates.

### Build

```text
dotnet build IdeaCadConnector.sln
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:11.04
```

### Tests

```text
dotnet test IdeaCadConnector.sln
Passed!  - Failed: 0, Passed: 674, Skipped: 0, Total: 674, Duration: 5 s
```

### Accepted DEMO Package Integrity

```text
IcsCount: 88
IcsBytes: 30359552
Manifest Definitions: 88
Manifest Occurrences: 88
Missing manifest definition files: 0
Root: cad/DEMO__ROOT__DEMO.ics (2371584 bytes)
Manifest: pdm-bom-manifest.json (67690 bytes)
```

Runtime evidence and remaining acceptance gates are recorded in `runtime-evidence-2026-07-17.md`.
