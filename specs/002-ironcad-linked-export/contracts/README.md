# Contracts: IronCAD Linked Normalized Export

**Date**: 2026-07-16

## Public API Surface

This feature modifies **internal implementation** of the normalize/export pipeline. No new public API contracts are introduced.

### Changed Interfaces (internal)

| Contract | File | Change |
|----------|------|--------|
| `IronCadSceneNormalizationWriter.Export()` | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | Behavior change: produces external-linked root instead of embedded root |
| `IronCadNativeSaveAllExternalInvoker.Execute()` | Same | NEW deep module — one-parameter internal interface that invokes IronCAD 2025 native command ID 53046, supplies `destinationDirectory` to the modal folder dialog, and reports timeout/dialog failures. Confirmed on English IronCAD 2025. |
| `IronCadSceneNormalizationWriter.Apply()` | Same | Unchanged signature; may add shared-definition tracking |
| `IronCadExportPackageVerifier.VerifyExternalLinks()` | Same | NEW — `(IZSceneDoc, PdmNormalizationPlan, IronCadExternalReferenceValidationContext context)` — orchestrates raw Reader + pure Validator. The command builds the context from package/output paths. Operates on already-open doc. |
| `IIronCadSceneDocumentService` (new interface) | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | NEW — injectable seam for command testability: `IZSceneDoc OpenDocument(string path)`, `void CloseDocument()`, extends `IDisposable`. Only one temporary document is open at a time; `CloseDocument()` is idempotent, closes the document from the latest successful open, and restores the prior active document. The command calls it from `finally`; verifier/validator receive an already-open doc. |
| `ElementId` (new struct) | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | NEW — opaque value-type identity token (`int`-backed, `IEquatable<ElementId>`); stored in `IronCadSceneSnapshot.ElementIds` (separate from `Elements` which keeps `IZElement` for COM ops) |
| `IronCadDefinitionFileMapBuilder.Build()` | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | NEW — receives `IReadOnlyDictionary<PdmSourceNode, ElementId>`, groups by shared `ElementId`; returns `IDictionary<PdmSourceNode, string>` |
| `IronCadExportResult` (new class) | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | NEW — result DTO: `RootFilePath` + `SourceNodeToDefFileMap` |
| `IronCadExternalReferenceRecord` | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | **MODIFY** — extracted from `IronCadExportPackageVerifier.cs`; 7-field DTO shared by Reader (sets `OccurrencePath`, `ReportedLinkPath`) and Validator (sets `ResolvedTargetPath`, `Exists`, `InsidePackage`, `PointsToSource`, `CanonicalFileNameMatch`) |
| `IronCadExternalReferenceReader.Read()` | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | NEW — raw-only COM adapter: reads `IZSceneDoc`, produces one record per occurrence, with `OccurrencePath` + `ReportedLinkPath` only. Unlinked occurrences are emitted with null/empty `ReportedLinkPath`; there are no file-existence checks or plan comparisons. MUST run on IronCAD's STA thread; no cross-apartment COM marshaling. `[COM automation]` |
| `IronCadExternalReferenceValidator.Validate()` | `src/IdeaCadConnector.IronCAD/NormalizeExport/` | NEW — pure logic (no COM): receives `IReadOnlyList<IronCadExternalReferenceRecord>` + `PdmNormalizationPlan` + `IronCadExternalReferenceValidationContext`. Passes raw `ReportedLinkPath` to `Evaluate(ReportedLinkPath, DocumentDirectory, CadRoot, SourceRoot, StagingRoot, expectedFileName)`; the policy resolves and returns `ResolvedTargetPath`. Excludes root path `"0"` and enforces exact non-root occurrence-set validation (missing/duplicate/unexpected). Tested with `[xUnit+fakes]`. |
| `PdmManifestV2Factory.Create()` | `src/IdeaCadConnector.Workspace/NormalizeExport/` | Add `definitionFile` field to `PdmManifestOccurrence`; accept `IDictionary<PdmSourceNode, string>` param; deduplicate `Definitions[]` by definition file (driven by `sourceNodeToDefFileMap`, not ElementId); assign shared `DefinitionId` to all occurrences with same file; update `BomV2` child refs; detect DefinitionId collisions and conflicting metadata |
| `PdmPackageManifestWriter` | Same | Serialize new `DefinitionFile` field on occurrence entries |

### Unchanged Interfaces

| Contract | Reason |
|----------|--------|
| `IPdmRepositoryClient` | Push preview/existence unchanged |
| `IArasCadClient` | Checkout/check-in unchanged |
| `ICadApplicationAdapter` | Read-only CAD open unchanged |
| `PdmNormalizationPlanner` | Plan creation unchanged |
| `NormalizeExportDialog` | Preview dialog unchanged |
| `PdmOutputSafetyValidator` | Output validation unchanged |
| `PdmExternalReferencePolicy` | Link evaluation policy unchanged |

## ICAPI Interop Contract

All IronCAD-specific calls go through `interop.ICApiIronCAD` (IronCAD 2025, COM interop). The feature depends on:

| API | Usage | Status |
|-----|-------|--------|
| `IZSceneDoc.SaveAs(string path, eZLinksSaveOptions, bool)` | Stage canonical root with `Z_LINKS_IGNORE`; persist approved names/properties and native links with `Z_LINKS_SAVE_ALL` | ✅ Runtime-confirmed |
| Native `WM_COMMAND` ID `53046` | Execute the same `Save All As External` operation as the IronCAD ribbon | ✅ Runtime-confirmed on IronCAD 2025 |
| `IZSceneDoc.ImportFile(string path, bool)` / `Shapes.Add(string path)` | Synthetic fixture creation only | ⚠️ Rejected for production DEMO reconstruction because geometry was blank/missing |
| `IZSceneElement.ModelLinkPath` (getter) | Read external link path | ✅ Unchanged |
| `IZPart.GetExternallyLinkedInfo(out bool)` | Check external link status | ✅ Unchanged |
| `IZAssembly.GetExternallyLinkedInfo(out bool)` | Check external link status | ✅ Unchanged |
| `IZElement.GetChildrenZArray()` | Traverse scene tree | ✅ Unchanged |
| `IZElement.GetCustomPropManager(int)` | Read/write PDM properties | ✅ Unchanged |
| `IZCustomPropMgr.AddCustomPropString(...)` | Write custom property | ✅ Unchanged |

## Manifest Contract

The existing `PdmPackageManifest` (SchemaVersion=2) already uses `Definitions[]` + `Occurrences[]` + `BomV2[]`. The linked export feature does NOT restructure this — it changes how entries are populated:

| Change | Before (current) | After (linked export) |
|--------|------------------|----------------------|
| `Occurrence.DefinitionFile` | absent | present — links occurrence to its `.ics` file |
| `Definitions[]` cardinality | one per plan item | one per unique definition file (deduplicated) |
| `Occurrence.DefinitionId` | unique per occurrence path | shared across occurrences of the same ElementId (reader group) |
| `BomV2.ChildDefinitionId` | unique per child occurrence | deduplicated — same ID for same definition |

See [data-model.md](../data-model.md) § PDM Manifest for full details.

## Error Contract

All errors use existing `PdmNormalizeExportException` with error codes:
- `EXTERNAL_EXPORT_FAILED` — child cannot be saved as `.ics`
- `PACKAGE_VALIDATION_FAILED` — missing/mismatched files in package
- `ROUND_TRIP_VALIDATION_FAILED` — external link mismatch on re-open
- `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` — external dependencies in source (unchanged)
- `SCENE_TRAVERSAL_FAILED` — cannot read/write scene tree

No new error codes needed.
