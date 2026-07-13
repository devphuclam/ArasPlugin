# BOM-00 — IronCAD ICAPI capability report

Status: `BLOCKED_RUNTIME_VERIFICATION`

## Baseline and environment

| Item | Evidence |
|---|---|
| Repository baseline | `origin/main` commit `ee23a38` |
| IronCAD executable | `C:\Program Files\IronCAD\2025\bin\IRONCAD.exe`, file version `27.0.26.19811` |
| Primary interop | `interop.ICApiIronCAD.dll`, assembly/file version `27.0.0.0` |
| Companion interop | `IronCADCOMInterop.dll`, assembly version `27.0.0.0` |
| Study | `PDM_StudyCase_260713-1.ics` exists locally; original was not opened for writing, modified, renamed, or committed |
| Runtime probe | `BLOCKED_RUNTIME_VERIFICATION`; disposable study copy opened and legacy `IronCAD.Application` was active, but the project add-in was not loaded, so no `_addinSite.Application`/`IZBaseApp` seam was available |

Reflection and SDK sample evidence establishes only `API_PRESENT`. It does not establish runtime behavior, persistence, occurrence reuse, quantity semantics, or equivalence to UI externalization.

## Capability matrix

Classification values are exactly: `API_PRESENT`, `RUNTIME_VERIFIED`, `PARTIALLY_VERIFIED`, `NOT_VERIFIED`, `NOT_SUPPORTED`, and `BLOCKED`.

| # | Capability | Classification | Interop type/member and exact signature | Compile-time evidence | Runtime evidence | Limit/follow-up |
|---:|---|---|---|---|---|---|
| 1 | Obtain active `IZSceneDoc` | API_PRESENT | `IZBaseApp.ActiveDoc : IZDoc` | Reflection and SDK sample `GetActiveDoc()` | BLOCKED_RUNTIME_VERIFICATION | Cast must be observed on active study |
| 2 | Retrieve root/top element | API_PRESENT | `IZSceneDoc.GetTopElement() : IZElement` | Reflection and `BOMPropExample` | BLOCKED_RUNTIME_VERIFICATION | Top element availability unknown |
| 3 | Enumerate direct children | API_PRESENT | `IZElement.GetChildrenZArray() : IZArray`; `IZArray.Count(out int) : void`; `IZArray.Get(int,out object) : void` | Reflection and SDK sample | BLOCKED_RUNTIME_VERIFICATION | Child collection behavior unknown |
| 4 | Recursively enumerate descendants | API_PRESENT | Repeated `IZElement.GetChildrenZArray()` / `IZArray.Get(...)` | SDK sample recursively traverses assemblies | BLOCKED_RUNTIME_VERIFICATION | Cycle/reuse semantics unknown |
| 5 | Distinguish assembly/part/technical nodes | API_PRESENT | `IZElement.Type : eZElementType`; enum includes `Z_ELEMENT_PART`, `Z_ELEMENT_ASSEMBLY`, BREP, wire, profile and technical types | Reflection enum values | BLOCKED_RUNTIME_VERIFICATION | Study distribution unknown |
| 6 | Retrieve visible Scene Browser name | API_PRESENT | `IZElement.GetTreeViewDisplayShapeName(bool,bool,out string) : void`; also `Name : string` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Must compare with visible browser |
| 7 | Retrieve source/external filename | API_PRESENT | `IZSceneElement.ModelLinkPath : string`; `IZPart.GetExternallyLinkedInfo(out bool) : string`; `IZAssembly.GetExternallyLinkedInfo(out bool) : string` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Authoritative member per node type unknown |
| 8 | Retrieve parent | API_PRESENT | `IZElement.GetParent() : IZElement` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Root/linked parent behavior unknown |
| 9 | Retrieve child order/find-number order | API_PRESENT | `IZArray.Get(int,out object) : void` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Indexed order is not proven BOM/find-number order |
| 10 | Detect suppressed/hidden/excluded nodes | API_PRESENT | `IZElement.GetStateStatus(eZElementState) : bool`; `IZPart.IsHidden : bool`; `IZAssembly.IsHidden : bool`; `IncludedInBOM : bool` | Reflection | BLOCKED_RUNTIME_VERIFICATION | State semantics and combinations unknown |
| 11 | Persistent ICAPI element identifier | API_PRESENT | `IZElement.Id : int` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Runtime ID persistence unknown |
| 12 | ID survives save/close/reopen/rename/externalization | NOT_VERIFIED | No persistence contract; `IZElement.Id : int` is only a candidate | Reflection cannot prove persistence | BLOCKED_RUNTIME_VERIFICATION | Requires controlled manual study-copy procedure |
| 13 | Custom properties on every node | API_PRESENT | `IZElement.GetCustomPropManager(int) : IZCustomPropMgr`; `IZCustomPropMgr.Count : int` | Reflection and SDK sample reads per element | BLOCKED_RUNTIME_VERIFICATION | Per-node availability/persistence unknown |
| 14 | Store PDM custom properties safely | API_PRESENT | `IZCustomPropMgr.AddCustomPropString(string,string,eZPropPersFlag,bool) : void`; `AddCustomPropEx(...) : void` | Reflection only; probe does not call setters | BLOCKED_RUNTIME_VERIFICATION | No writes permitted in BOM-00; persistence unknown |
| 15 | Distinguish reused occurrence from independent part | API_PRESENT | `IZSceneElement.IsSameModelByGUID(eZModelCompT,IZElement,eZModelCompT) : bool` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Applicability and identity semantics unknown |
| 16 | Group repeated occurrences for quantity | API_PRESENT | `IZSceneElement.GetInternallyLinkedElements() : object`; `GetInternallyLinkedElementsCount() : int` | Reflection | BLOCKED_RUNTIME_VERIFICATION | No runtime repeated-occurrence evidence |
| 17 | Separate definition identity from occurrence identity | NOT_VERIFIED | No explicit definition/occurrence pair found; `IZDoc.FileGUID : string` is document-level only | Reflection inventory | BLOCKED_RUNTIME_VERIFICATION | Must not derive from names or filenames |
| 18 | Call “Save All as External” through ICAPI | API_PRESENT | `IZSceneDoc.SaveAs(string,eZLinksSaveOptions,bool) : void`; enum includes `Z_LINKS_SAVE_ALL` | Reflection | Not called by read-only probe | UI equivalence intentionally unverified; defer manual procedure |
| 19 | Select output folder programmatically | API_PRESENT | `IZSceneDoc.SaveAs(string,eZLinksSaveOptions,bool) : void` accepts target path | Reflection | Not called | Child output-folder behavior unknown |
| 20 | Control every external filename | NOT_VERIFIED | No per-child filename member found in inspected interfaces | Reflection inventory | Not called | Requires separate manual externalization test |
| 21 | Filename derived from Scene Browser name | NOT_VERIFIED | No member proves this naming rule | Reflection inventory | Not called | Requires manual observation |
| 22 | Overwrite/version/skip behavior | API_PRESENT | `SaveAs(...,bool vbForceOverwriteExisting)` exposes overwrite flag | Reflection | Not called | Child-file collision behavior unknown |
| 23 | Preserve root/child references | API_PRESENT | `eZLinksSaveOptions.Z_LINKS_SAVE_ALL` is accepted by `SaveAs` | Reflection | Not called | Reference preservation unverified |
| 24 | Relative/absolute/search-path references | API_PRESENT | `IZSceneElement.ModelLinkPath : string` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Path form unverified |
| 25 | Read reference path after externalization | API_PRESENT | `IZSceneElement.ModelLinkPath : string`; `GetExternallyLinkedInfo(out bool)` | Reflection | Not called | Requires disposable-copy manual test |
| 26 | Relink missing external reference | API_PRESENT | `IZTDLinkedScene.ChangeSource(string,bool) : void` | Reflection | Not called | Applicability to scene nodes unverified |
| 27 | Open pulled workspace from different root | NOT_VERIFIED | No repository or inspected single-method validator | Inventory | Not run | Defer to BOM-08-style relocation test |
| 28 | Validate root assembly after relocation | NOT_VERIFIED | No inspected validator contract | Inventory | Not run | Requires aggregate tree comparison |
| 29 | Read part number/description/material/revision/mass/state/type | API_PRESENT | `IZPart.BOMPartNumber : string`; `BOMDescription : string`; `BOMQuantityString : string`; `IZPartProperty.GetMaterialName(out string,out bool) : void`; `CalculatedMass : double`; `GetMassProperties(...) : void`; `IZElement.Type : eZElementType` | Reflection | BLOCKED_RUNTIME_VERIFICATION | Revision field not found in inspected part/assembly interfaces |
| 30 | Native BOM table/API | API_PRESENT | `IZSceneDoc.ExportBOM(string) : void`; `IZSceneElement.ExportBOM(string) : void`; `ExportSelfBOM(...) : void`; `IZBaseApp.ExportBOMData(...) : void` | Reflection | Not called | Read-only probe deliberately does not export |
| 31 | Construct accurate BOM from Scene Tree | API_PRESENT | `GetTopElement`, `GetChildrenZArray`, `Type`, parent/child APIs | Reflection and pure analyzer tests | BLOCKED_RUNTIME_VERIFICATION | Accuracy depends on identity/reuse evidence |
| 32 | Preserve multi-level hierarchy | API_PRESENT | Recursive `GetChildrenZArray` traversal | Reflection and pure analyzer tests | BLOCKED_RUNTIME_VERIFICATION | Study hierarchy not observed |
| 33 | Determine quantity per parent | NOT_VERIFIED | Pure analyzer supports it only with supplied definition identity | Reflection cannot prove definition identity | BLOCKED_RUNTIME_VERIFICATION | Quantity remains `IdentityUnavailable` without identity candidate |

## Implemented diagnostic architecture

- `IdeaCadConnector.Workspace.BomDiagnostic` contains provider-neutral source nodes, analyzed nodes, quantity status, pure traversal, sanitizer and local output writer.
- `IdeaCadConnector.IronCAD.BomDiagnostic` contains the only ICAPI reader and probe.
- `IronCadAddin.RunBomDiagnosticProbe(...)` is a DEBUG-only internal seam, not a production command or ribbon feature.
- The reader requires an active scene, reads only the approved read operations, and records optional COM failures as warnings.
- Local raw output includes a proprietary-metadata warning and uses `FileMode.CreateNew`. Committed/public evidence is aggregate-only.

## RED/GREEN evidence

| Stage | Command/result |
|---|---|
| RED | `dotnet build tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj --configuration Debug --no-restore -m:1` failed because `BomDiagnostic` types did not exist; 3 expected compiler errors |
| GREEN analyzer | Same serialized build passed, 0 errors |
| GREEN focused | `dotnet test ... --no-restore --no-build --filter FullyQualifiedName~BomDiagnosticTreeAnalyzerTests` passed: 14/14 |
| Runtime probe attempt | `BLOCKED_RUNTIME_VERIFICATION`; study copy opened successfully, but `IdeaCadConnector.IronCAD` was absent from the IronCAD process and no active add-in-provided `IZBaseApp` was available |

## Runtime verification record

```text
Runtime probe status: BLOCKED_RUNTIME_VERIFICATION
IronCAD version: 27.0.26.19811
Interop assembly version: 27.0.0.0
Active legacy page: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\ARAS-Plugin\IdeaCadConnector\.ai-work\BOM-00-runtime\study-copy.ics`
Active document type: NOT OBSERVED (add-in not loaded)
Top element available: NOT OBSERVED
Total traversed nodes: NOT OBSERVED
Assembly nodes: NOT OBSERVED
Part nodes: NOT OBSERVED
Technical/unknown nodes: NOT OBSERVED
Maximum depth: NOT OBSERVED
Repeated definition candidates: NOT OBSERVED
Quantity calculation status: IdentityUnavailable / runtime blocked
External-link nodes: NOT OBSERVED
Suppressed nodes: NOT OBSERVED
Hidden nodes: NOT OBSERVED
Excluded-from-BOM nodes: NOT OBSERVED
Warnings: study copy was open through legacy `IronCAD.Application`, but the project add-in was not loaded; direct legacy COM `ZIronCADApp` access could not substitute for the add-in's typed `_addinSite.Application` (`TYPE_E_LIBNOTREGISTERED`/no `IZBaseApp` QI)
```

## Safety and out-of-scope

The implementation does not call any CAD write/export/relink method, Aras API, schema/server method, Naming Policy code, Desktop PDM code, production manifest code, Push, Pull, or externalization automation. It does not modify or commit the supplied study or raw diagnostic JSON.

BOM-01 must not begin until a fresh runtime probe verifies scene traversal and node classification, and identity/reuse/quantity uncertainty is explicitly resolved or deferred with evidence.
