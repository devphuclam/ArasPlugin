# Implementation Plan: IronCAD Linked Normalized Export

**Branch**: `002-ironcad-linked-export` | **Date**: 2026-07-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-ironcad-linked-export/spec.md`

## Summary

Modify `IronCadSceneNormalizationWriter.Export()` so the exported root contains the same hierarchy, geometry, transforms, and true external-file relationships produced by IronCAD 2025's native `Save All As External`, while applying canonical PDM filenames and metadata. The runtime-approved flow stages the root, temporarily applies canonical definition-name stems, invokes the native command for package `cad/`, verifies emitted files, restores approved names/properties, and saves all links. It does not rebuild the scene. No Aras schema, preview dialog, or external-dependency-blocking changes.

**Remaining hard gates**: dedicated shared-definition identity/dedup (T006), numeric transform comparison (T005), and SHA256 save-through-root UAT (T007/T036) remain required before declaring all acceptance criteria complete. The accepted DEMO runtime confirms native-equivalent hierarchy, visible geometry, canonical files, and external links.

## Technical Context

**Language/Version**: C# — .NET Framework 4.8 (`net48`), strong-name signed, COM-visible for IronCAD add-in

**Primary Dependencies**:
- `interop.ICApiIronCAD` (IronCAD 2025, ICAPI) — part/assembly save, scene traversal, link read
- `IdeaCadConnector.Workspace` — `PdmNormalizationPlan`, `PdmNameNormalizer`, `PdmPackageManifestWriter`, `PdmExternalReferencePolicy`, `PdmRoundTripPlanComparer`, `PdmOutputSafetyValidator`
- `IdeaCadConnector.IronCAD` — existing `IronCadSceneNormalizationWriter`, `IronCadSceneNormalizationReader`, `IronCadExportPackageVerifier`, `IronCadNormalizeExportCommand`

**Storage**: Local filesystem (exported package directory: `cad/*.ics` + `pdm-bom-manifest.json`)

**Testing**:
- xUnit (`IdeaCadConnector.Tests`, `tests/IdeaCadConnector.Tests/`) — unit tests with fakes for `PdmNormalizationPlanner`, `PdmNameNormalizer`, `PdmOutputSafetyValidator`, manifest factory, `IronCadDefinitionFileMapBuilder` (via `ElementId` value-type identity), `IronCadExternalReferenceValidator`, `IIronCadSceneDocumentService` (command seam), manifest tests, and manifest cross-reference regression tests
- IronCAD runtime tests (`[IronCAD runtime]`) — writer implementation (T025–T027), US3 integration (T035), Phase 0 T1–T6, and manual UAT (T036); require active IronCAD session
- COM automation tests (`[COM automation]`) — `IronCadExternalReferenceReader` (T030), `IronCadExportPackageVerifier.VerifyExternalLinks` (T032), `IIronCadSceneDocumentService` real implementation (T033); driven via `interop.ICApiIronCAD` in isolated process
- Manual UAT (T036) — FR-013 (edit child through root → SHA256 change in child file)

**Target Platform**: Windows x64, IronCAD 2025 host (ICAPI `interop.ICApiIronCAD`), no server component

**Project Type**: Desktop application add-in (IronCAD COM add-in), with shared library (`Workspace`) for domain logic

**Performance Goals**:
- Round-trip verification under 5 seconds for 50 children (SC-004)
- Export block within 2 seconds for dangling external links (SC-005)

**Constraints**:
- .NET Framework `net48` only — no .NET Core/CoreCLR
- Must preserve strong-name signing, COM registration, WPF dialog compatibility
- Must not change Aras schema, IOM, remote checkout protocol, or `ArasCadClient`
- Must not add new NuGet dependencies or projects
- Must not lift the existing `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` hard block

**Scale/Scope**: Single-user local export, typically <100 child components per scene

**Baseline note**: Baseline verification (`dotnet build`, `dotnet test`, `git status`) captures the working-tree state as-is. Pre-existing uncommitted changes unrelated to this feature are recorded but not discarded. `git stash` is NOT used.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Evidence |
|-----------|-------|----------|
| I. Sources of Truth | ✅ Spec + plan + tasks in `specs/002-` | Spec exists and is complete |
| II. Architecture Boundaries | ✅ No new projects, no dependency cycles | Changes limited to `IronCAD/NormalizeExport/` and `Workspace/NormalizeExport/` |
| III. Aras Domain Safety | ✅ No Aras schema changes | FR-011 explicitly prohibits |
| IV. Compatibility | ✅ net48, WPF, COM, strong-name preserved | No toolchain changes; existing build targets untouched |
| V. Testing & Verification | ✅ FR-013 mandates UAT; existing test suite must pass (SC-006) | Phase 3 (tasks.md) adds TDD tests for manifest and export result; Phase 4 implements |
| VI. Spec-Driven | ✅ Plan follows canonical artifacts | All artifacts in `specs/002-` |
| VII. Review | ✅ Implementer does not self-approve | External review required after implementation |

**Gate verdict**: PASS — no violations. Complexity justification not required.

### Phase 0 Hard Gates

The Phase 0 runtime test suite (T1–T6, defined in `research.md`) remains the acceptance record. Runtime investigation selected the native-command approach; no Outcome A/Outcome B reconstruction path remains in production.

| Gate | Test | Pass/Fail Decision |
|------|------|--------------------|
| **G1. IZElement shared-definition identity** | T3 | If `object.ReferenceEquals` does NOT correctly identify shared IZElement occurrences → **block linked export with error**. No fallback to `ItemCode` equivalence. Feature does NOT degrade to one-file-per-occurrence. |
| **G2. Transform preservation** | T2 | Native output visibly preserved the DEMO design, but the dedicated numeric transform comparison remains required. Any mismatch blocks release. |
| **G3. Manifest field name** | (design) | The occurrence-to-definition link field is finalized as **`definitionFile`** (C# property `DefinitionFile`, JSON key `"definitionFile"`). Added to `PdmManifestOccurrence`. |

If G1 or G2 fails, the feature is not release-ready even though the current runtime-approved implementation remains available for further validation. The failure and evidence must be recorded in `research.md`.

### Deep Module: `IronCadNativeSaveAllExternalInvoker`

This deep module owns the native IronCAD interaction behind one interface:

```csharp
internal sealed class IronCadNativeSaveAllExternalInvoker
{
    public void Execute(string destinationDirectory);
}
```

Its implementation locates the current IronCAD window, invokes native command ID `53046`, selects the destination in IronCAD's modal folder dialog, and handles timeout/failure cleanup. The writer knows none of those Win32 details. This seam gives callers leverage and keeps native UI knowledge local. The confirmed runtime is English IronCAD 2025; localized dialog captions are an explicit unvalidated constraint.

### Deep Module: `IronCadDefinitionFileMapBuilder`

The IZElement-dedup-to-PdmSourceNode-map logic lives in a dedicated builder. The snapshot carries BOTH raw IZElement handles (for COM operations like `Apply()` / `SaveAs()`) AND opaque `ElementId` tokens (for dedup grouping). The builder receives only the identity tokens — this keeps tests free of COM.

```csharp
// IronCAD layer — value-type, no COM
public readonly struct ElementId : IEquatable<ElementId>
{
    private readonly int _id;
    public ElementId(int id) => _id = id;
    // IEquatable<ElementId> via _id equality
}

// Snapshot has two dictionaries:
//   Elements    — IReadOnlyDictionary<PdmSourceNode, IZElement>   (for COM)
//   ElementIds  — IReadOnlyDictionary<PdmSourceNode, ElementId>   (for dedup)

public sealed class IronCadDefinitionFileMapBuilder
{
    public IDictionary<PdmSourceNode, string> Build(
        IReadOnlyDictionary<PdmSourceNode, ElementId> elementIds,
        PdmNormalizationPlan plan);
}
```

Responsibilities:
- Group `PdmSourceNode` entries by shared `ElementId`
- Select canonical filename from the first plan item per group
- Report missing identity (plan item not in dictionary) and ambiguity (same ElementId → different ItemCodes)

### Result DTO: `IronCadExportResult`

`IronCadSceneNormalizationWriter.Export()` returns an `IronCadExportResult` instead of `void`. This is a simple DTO with no grouping logic:

```csharp
public sealed class IronCadExportResult
{
    public string RootFilePath { get; init; }
    public IDictionary<PdmSourceNode, string> SourceNodeToDefFileMap { get; init; }
}
```

The command (`IronCadNormalizeExportCommand`) receives the result, passes `SourceNodeToDefFileMap` to `PdmManifestV2Factory.Create(plan, map)`, and uses `RootFilePath` for round-trip verification.

### Manifest Changes (Full)

`PdmManifestV2Factory.Create()` receives the export-local PdmSourceNode→definition-file mapping via the **`sourceNodeToDefFileMap` parameter** (`IDictionary<PdmSourceNode, string>`). When non-null, the factory applies these changes:

1. **`DefinitionFile` on occurrences**: Each `PdmManifestOccurrence` gets a new `DefinitionFile` property (JSON key `"definitionFile"`) set from the map.
2. **Deduplicated `Definitions[]`**: Instead of one definition per plan item (current behavior via `GetDefinitionId()` using occurrence path), one `PdmManifestDefinition` per unique definition file. The mapping is driven by the map, not by occurrence path.
3. **Shared `DefinitionId`**: All occurrences whose `SourceNode` maps to the same definition file receive the same `DefinitionId`. The `Id` is derived from the definition file name (e.g., `"def-" + canonicalFileName.Replace(...)`).
4. **Updated `BomV2`**: `BomV2` entries reference the deduplicated `ChildDefinitionId` — child occurrences of the same definition share the same ID.
5. **Backward compat**: When `sourceNodeToDefFileMap` is null, the factory falls back to current behavior (one definition per plan item, occurrence-path-based IDs).

**Architecture boundary enforcement**: The mapping is computed in the IronCAD layer by `IronCadDefinitionFileMapBuilder` (groups `PdmSourceNode` by shared `ElementId` via `IronCadSceneSnapshot.ElementIds`). No COM/IZElement types cross into Workspace.

The mapping is ephemeral (export-session only) and is returned as part of `IronCadExportResult` from the export writer. See `data-model.md` for full pseudocode.

### Round-Trip Verification Contract

The command owns document open/close via `IIronCadSceneDocumentService`. The verifier and validator operate on an **already-open** document. This keeps file I/O in the orchestration layer and verification pure.

Document-service lifecycle invariants:
- Only one temporary exported-root document may be open at a time.
- `CloseDocument()` closes exactly the document returned by the latest successful `OpenDocument()` and restores the previously active document (or the empty-scene state).
- `CloseDocument()` is idempotent.
- `OpenDocument()` throws on failure without leaving a dangling document or COM reference.
- The command calls `CloseDocument()` from `finally`; `Dispose()` is reserved for service-level COM cleanup.

- **Verifier**: `VerifyExternalLinks(IZSceneDoc, PdmNormalizationPlan, IronCadExternalReferenceValidationContext context)` — orchestrates raw Reader + pure Validator.
- **Validator**: `Validate(IReadOnlyList<IronCadExternalReferenceRecord>, PdmNormalizationPlan, IronCadExternalReferenceValidationContext context)` — pure logic, no COM. The context struct bundles `DocumentDirectory`, `PackageRoot`, `CadRoot`, `SourceRoot`, `StagingRoot` — preserving existing isolation checks without six individual string parameters.
- The Reader produces raw link observations only (OccurrencePath, ReportedLinkPath — no plan knowledge, no file-existence check). The Validator enriches the record (ResolvedTargetPath, Exists, InsidePackage, PointsToSource, CanonicalFileNameMatch), resolves expected canonical names from the plan, checks path boundaries, checks file existence, and enforces exact occurrence-set validation (missing, duplicate, and unexpected OccurrencePath all fail).

## Project Structure

### Documentation (this feature)

```text
specs/002-ironcad-linked-export/
├── spec.md              # Feature specification
├── plan.md              # This file (/speckit.plan)
├── research.md          # Phase 0 — ICAPI runtime validation
├── data-model.md        # Phase 1 — entity definitions
├── quickstart.md        # Phase 1 — runnable validation guide
├── contracts/           # Phase 1 — interface contracts
└── tasks.md             # Implementation tasks (42 tasks, 6 phases)
```

### Source Code (affected files only)

```text
src/IdeaCadConnector.IronCAD/
├── NormalizeExport/
│   ├── IronCadSceneNormalizationWriter.cs    # MODIFY — Export() returns IronCadExportResult
│   ├── IronCadDefinitionFileMapBuilder.cs    # NEW — ElementId-dedup → PdmSourceNode map
│   ├── IronCadExportResult.cs                # NEW — result DTO (RootFilePath + SourceNodeToDefFileMap)
│   ├── ElementId.cs                           # NEW — opaque value-type identity token (int-backed, IEquatable<ElementId>)
│   ├── IronCadExternalReferenceRecord.cs     # MODIFY — extracted from IronCadExportPackageVerifier.cs; 7-field DTO shared by Reader (raw) and Validator (enriched)
│   ├── IIronCadSceneDocumentService.cs        # NEW — seam interface (IZSceneDoc OpenDocument + IDisposable)
│   ├── IronCadExternalReferenceReader.cs     # NEW — COM adapter: traverses IZSceneDoc, produces records
│   ├── IronCadExternalReferenceValidator.cs  # NEW — pure logic: validates records against plan
│   ├── IronCadExportPackageVerifier.cs       # MODIFY — VerifyExternalLinks(IZSceneDoc, plan, IronCadExternalReferenceValidationContext) orchestrates reader+validator
│   └── IronCadNormalizeExportCommand.cs      # MODIFY — receive IIronCadSceneDocumentService via ctor; use for open/close

src/IdeaCadConnector.Workspace/
├── NormalizeExport/
│   ├── PdmPackageManifestWriter.cs           # MODIFY — add definitionFile field to occurrence entries
│   ├── PdmManifestV2Factory.cs               # MODIFY — accept IDictionary<PdmSourceNode, string> param; dedup driven by sourceNodeToDefFileMap (not by ElementId or snapshot)
│   ├── PdmNormalizationPlanner.cs            # UNCHANGED — plan unchanged
│   └── PdmExternalReferencePolicy.cs         # UNCHANGED — policy already correct

tests/IdeaCadConnector.Tests/
├── PdmLinkedExportManifestTests.cs           # NEW — TDD tests for DefinitionFile, factory, missing-map, dedup, collisions, cross-reference regression
├── PdmLinkedExportDefinitionFileMapBuilderTests.cs  # NEW — TDD tests for builder (grouping, identity, ambiguity)
├── PdmLinkedExportVerifierTests.cs           # NEW — TDD tests for IronCadExternalReferenceValidator (xUnit+fakes)
├── PdmLinkedExportCommandTests.cs            # NEW — TDD tests for command with IIronCadSceneDocumentService fake (xUnit+fakes)
├── PdmIronCadAdapterTests.cs                 # MODIFY — add linked-export integration tests
```

## Complexity Tracking

Not required — Constitution Check passed without violations.
