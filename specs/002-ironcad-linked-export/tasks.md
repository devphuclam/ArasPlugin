---

description: "Implementation tasks for IronCAD Linked Normalized Export"
---

# Tasks: IronCAD Linked Normalized Export

**Input**: Design documents from `specs/002-ironcad-linked-export/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Remaining Hard Gates**:
- **G1 (T006/T3)**: dedicated shared-definition identity/dedup runtime fixture. Failure blocks release; no fallback by item code.
- **G2 (T005/T2)**: numeric occurrence-transform comparison. Failure blocks release.
- **G3 (T007/T036/T4)**: SHA256 save-through-root UAT required by FR-013.

The native implementation and accepted DEMO evidence are recorded, but unchecked gates remain release work and must not be reported as complete.

**Format**: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to user story (US1, US3). US2 and US4 were merged into US1 — shared-definition dedup and round-trip verification are required from the first implementation phase.
- **Test harness labels**:
  - `[xUnit+fakes]` — pure xUnit test with test doubles; no IronCAD runtime needed.
  - `[IronCAD runtime]` — requires a running IronCAD process with a scene open; uses COM automation.
  - `[COM automation]` — script driven via `interop.ICApiIronCAD`; isolated process + temp workspace per test.

---

## Phase 1: Setup

**Purpose**: Branch preparation and baseline evidence.

**⚠️ Dirty-tree note**: If the working tree has pre-existing uncommitted changes unrelated to this feature, record them in the baseline evidence but do not discard them. Baseline = `dotnet build` and `dotnet test` results plus `git status --short` captured verbatim.

- [x] T001 Create branch `002-ironcad-linked-export`; run `dotnet build IdeaCadConnector.sln` and `dotnet test IdeaCadConnector.sln`; record exact build output, test pass/fail/skip counts, and `git status --short` as baseline evidence in feature directory
- [x] T002 Save baseline evidence to `specs/002-ironcad-linked-export/baseline-evidence.md` (build log, test summary, status)

**Checkpoint**: Baseline captured. No regressions assumed — actual results govern.

---

## Phase 2: Foundational — Phase 0 ICAPI Research (FR-012)

**Purpose**: Run T1–T6 runtime tests in IronCAD and record the selected native externalization route plus remaining acceptance gates.
**Historical sequencing rule**: T003–T009 were designed as read-only runtime validation. The final in-process native decision and production evidence are now recorded under T004/T008–T010; unchecked runtime gates remain explicit.

### ICAPI Runtime Tests

**⚠️ Sequential unless isolated**: T004–T009 SHARE the same IronCAD process and workspace by default. Run them sequentially (T003 first, then T004→T009 in order). Parallel execution requires each test to spawn its own `IRONCAD.exe` process with a temp workspace — label with `[COM automation]` if that approach is chosen.

- [x] T003 Create Phase 0 validation script at `tools/Phase0-IronCAD-Validation/run-tests.ps1` executing T1–T6 via IronCAD COM automation `[COM automation]`
- [x] T004 Run **T1 (Hierarchy preservation)** against production `DEMO.ics`: compare native `Save All As External` output with plugin output; verify the reopened canonical root retains the source hierarchy, visible geometry, and external child relationships `[IronCAD runtime]`
- [ ] T005 Run **T2 (Transform preservation)**: Record source transforms for 3 parts at different positions; export; compare positions/rotations in opened root `[IronCAD runtime]`
- [ ] T006 Run **T3 (IZElement dedup hypothesis)**: Scene with 1 part at 3 positions; assert `ReferenceEquals` across occurrences; verify 2 .ics files on export `[IronCAD runtime]`
- [ ] T007 Run **T4 (Save-through-root)**: Export 3-level hierarchy; edit child through root; save; verify child SHA256 changed `[IronCAD runtime]`
- [x] T008 Run **T5 (Custom property round-trip)**: accepted DEMO export completed property re-application, reopen, plan comparison, and package validation without a property mismatch `[IronCAD runtime]`
- [x] T009 Run **T6 (External link isolation)**: accepted DEMO package passed external-reference validation and was retained; all emitted definition files are under package `cad/` `[IronCAD runtime]`
- [x] T010 Record the selected native `Save All As External` approach, accepted DEMO evidence, rejected reconstruction/relink approaches, and remaining acceptance gates in `specs/002-ironcad-linked-export/research.md`

**Gate check**: If G1 or G2 blocks → STOP. Only `research.md` is updated. All subsequent tasks are cancelled.

### Manifest Model Preparation (AFTER gates pass)

These tasks produce production source changes and MUST NOT start until T010 confirms gates pass.

- [x] T011 Add `DefinitionFile` property (string, JSON key `"definitionFile"`) to `PdmManifestOccurrence` in `src/IdeaCadConnector.Workspace/NormalizeExport/PdmNormalizationModels.cs`
- [x] T012 Refactor `PdmManifestV2Factory.Create()` in `src/IdeaCadConnector.Workspace/NormalizeExport/PdmManifestV2Factory.cs`:
  - Accept `IDictionary<PdmSourceNode, string> sourceNodeToDefFileMap` parameter (null for backward compat)
  - Replace `GetDefinitionId()` logic: when map is non-null, assign the same `DefinitionId` to all occurrences whose `SourceNode` maps to the same definition file; when null, fall back to current occurrence-path-based ID
  - Deduplicate `Definitions[]` — one `PdmManifestDefinition` per unique definition file (not one per plan item)
  - Update `BomV2` child definition references to use the deduplicated `DefinitionId`
  - Keep existing `FileName` field derived from the canonical file name via the map
- [x] T013 Update `PdmPackageManifestWriter` in `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackageManifestWriter.cs` to serialize the new `DefinitionFile` field on occurrence entries

**Checkpoint**: Phase 0 outcome recorded. Manifest model supports:
- `definitionFile` on occurrences
- Deduplicated `Definitions[]` (one per unique file)
- Shared `DefinitionId` across multiple occurrences
- Updated `BomV2` child definition references
- Validator regression tests pass

---

## Phase 3: TDD — Failing Tests Before Implementation

**Purpose**: Write failing tests for each behavior change BEFORE implementation.
**⚠️ All tests MUST fail initially (red phase). They pass only after implementation tasks in Phase 4.**

### Manifest & Mapping Tests `[xUnit+fakes]`

- [x] T014 TDD: Write failing unit test in `tests/IdeaCadConnector.Tests/PdmLinkedExportManifestTests.cs` verifying that `PdmManifestOccurrence.DefinitionFile` is serialized as JSON key `"definitionFile"` and deserializes correctly
- [x] T015 TDD: Write failing unit test in `tests/IdeaCadConnector.Tests/PdmLinkedExportManifestTests.cs` verifying that `PdmManifestV2Factory.Create(plan, sourceNodeToDefFileMap)` sets `DefinitionFile` on each occurrence matching the map, and `null` map produces occurrences with `DefinitionFile == null`
- [x] T016 TDD: Write failing unit test in `tests/IdeaCadConnector.Tests/PdmLinkedExportManifestTests.cs` verifying that a plan item whose `SourceNode` is missing from the map produces an error or warning (missing-map failure)

### Deep Module: `IronCadDefinitionFileMapBuilder` `[xUnit+fakes]`

The builder receives `IReadOnlyDictionary<PdmSourceNode, ElementId>` (from `IronCadSceneSnapshot.ElementIds`). `ElementId` is an opaque value-type struct (`int`-backed, no COM dependency). The production Reader assigns one `ElementId` per unique `IZElement` (same IZElement → same id) and stores it in `snapshot.ElementIds` (keeping `snapshot.Elements` as `IReadOnlyDictionary<PdmSourceNode, IZElement>` for COM operations separately).

```csharp
// IronCAD layer — struct value-type, no COM types involved
public readonly struct ElementId : IEquatable<ElementId>
{
    private readonly int _id;
    public ElementId(int id) => _id = id;
    // IEquatable<ElementId> via _id equality
}

public sealed class IronCadDefinitionFileMapBuilder
{
    public IDictionary<PdmSourceNode, string> Build(
        IReadOnlyDictionary<PdmSourceNode, ElementId> elementIds,
        PdmNormalizationPlan plan);
}
```

- [x] T017 TDD: Write failing unit tests in `tests/IdeaCadConnector.Tests/PdmLinkedExportDefinitionFileMapBuilderTests.cs` for `IronCadDefinitionFileMapBuilder`:
  - Groups `PdmSourceNode` entries that share the same `ElementId` (construct `ElementId(1)`, `ElementId(1)`, `ElementId(2)` via dictionary fake)
  - Selects canonical filename by taking the first plan item's `CanonicalFileName` per group
  - Rejects / reports when a `PdmSourceNode` from the plan is absent from the dictionary (missing identity)
  - Reports when two plan items with different `ItemCode` map to the same `ElementId` (ambiguity)

### External-Reference Verification Split

External-link verification is split into two components to keep COM out of pure logic:

1. **`IronCadExternalReferenceReader`** (thin COM adapter) — reads `IZSceneDoc`, traverses every occurrence, and produces one `IronCadExternalReferenceRecord` per occurrence, including unlinked occurrences with a null/empty `ReportedLinkPath`. It sets only `OccurrencePath` and `ReportedLinkPath`; it does not check file existence, resolve paths, or compare against the plan.
2. **`IronCadExternalReferenceValidator`** (pure logic, no COM) — takes `IReadOnlyList<IronCadExternalReferenceRecord>` + `PdmNormalizationPlan` + `IronCadExternalReferenceValidationContext`: resolves paths relative to `DocumentDirectory`, validates package/source/staging isolation, resolves expected canonical names, checks target existence, and **ensures every expected child occurrence has exactly one matching record** (missing, duplicate, or unexpected links fail validation). Tested with `[xUnit+fakes]` by constructing DTO fixtures directly.
3. **`IronCadExportPackageVerifier.VerifyExternalLinks()`** — orchestrates on an already-open document (the command owns the open/close lifecycle). Calls reader, calls validator with directory context, returns result.

### Reader & Validator Tests `[xUnit+fakes]`

- [x] T018 TDD: Write failing unit tests in `tests/IdeaCadConnector.Tests/PdmLinkedExportVerifierTests.cs` for `IronCadExternalReferenceValidator`:
  - Detects `ReportedLinkPath` pointing outside `cadRoot` (feed DTO fixture with outside path, supply validation context)
  - Detects missing target file (DTO with `Exists == false`)
  - Detects canonical-filename mismatch between the plan's expected name for that occurrence and the filename part of the DTO's `ReportedLinkPath`
  - Detects an occurrence record with null/empty `ReportedLinkPath` as a missing-link failure
  - Detects **exact occurrence-set mismatch** after excluding the root occurrence (`plan.Root` / path `"0"`): a non-root plan OccurrencePath with no matching record (missing) — fail; a non-root record whose OccurrencePath is not in the plan (unexpected) — fail; two records with the same OccurrencePath (duplicate) — fail
  - Passes when every child occurrence in the plan has exactly one matching record, every record corresponds to a plan item, no duplicates exist, and all paths point inside `cadRoot`

### Manifest Conflicting-Metadata & Collision Tests `[xUnit+fakes]`

- [x] T019 TDD: Write failing unit tests in `tests/IdeaCadConnector.Tests/PdmLinkedExportManifestTests.cs`:
  - **Conflicting definition-level metadata**: The following fields are definition-level (must be identical across all occurrences sharing the same `DefinitionFile`): `ItemCode`, `DisplayName`, `ProjectCode`, `Revision`, `ItemType`. Two occurrences with the same `DefinitionFile` but differing values for any of these fields — factory reports an error or warning identifying the conflicting field and occurrence paths.
  - **DefinitionId collision**: Two distinct `DefinitionFile` values produce the same `DefinitionId` (e.g., after path normalization their IDs collide) — factory detects and reports the collision
  - **Path collision**: Two occurrences resolve to the same output file path through different plan items — factory rejects with duplicate-path error
  - **Canonical-name collision**: Two distinct plan items produce the same canonical child filename — planner/factory surfaces the duplicate-name warning with both occurrence paths; export cannot proceed until the collision is resolved

### Document Service Seam

`IronCadNormalizeExportCommand` receives `IIronCadSceneDocumentService` via constructor injection. This seam wraps the `IZSceneDoc` open/close lifecycle, making the command testable without a live IronCAD session:

```csharp
public interface IIronCadSceneDocumentService : IDisposable
{
    IZSceneDoc OpenDocument(string filePath);
    void CloseDocument();
}
```

Lifecycle invariants:
- Only one temporary document may be open at a time.
- `CloseDocument()` closes exactly the document returned by the latest successful `OpenDocument()`.
- `CloseDocument()` is idempotent and restores the previously active document (or the empty-scene state).
- `OpenDocument()` throws on failure without leaving a dangling document or COM reference.
- The command calls `CloseDocument()` from `finally`; service disposal is reserved for COM cleanup.

### Command Orchestration Tests `[xUnit+fakes]`

- [x] T020 TDD: Write failing unit tests in `tests/IdeaCadConnector.Tests/PdmLinkedExportCommandTests.cs` for `IronCadNormalizeExportCommand` using a fake `IIronCadSceneDocumentService`:
  - Opens exported root `.ics` via `_documentService.OpenDocument(rootPath)`
  - Calls `VerifyExternalLinks` on the opened doc
  - Disposes the service on success (verifies `Dispose()` was called)
  - On verification failure, does NOT leave a dangling `IZSceneDoc` (fake tracks open/close state)
  - On verification failure, removes the partial package directory and leaves the source/staging inputs unchanged
  - Verifies `CloseDocument()` is called from `finally` when verification or manifest validation fails
  - Verifies `OpenDocument()` failure does not trigger an invalid close of a document that was never opened

### Manifest Cross-Reference Regression Test `[xUnit+fakes]`

- [x] T021 TDD: Write failing unit test in `tests/IdeaCadConnector.Tests/PdmLinkedExportManifestTests.cs` (or `PdmPackageValidatorTests.cs`) verifying that a deduplicated manifest (shared `DefinitionId`, deduplicated `Definitions[]`, updated `BomV2`) passes all existing cross-reference checks: every `Occurrence.DefinitionId` references an entry in `Definitions[]`, and every `BomV2.ChildDefinitionId` references a valid definition

**Checkpoint**: T014–T021 all passing (green). Implementation and tests complete for all phase-3 items.

---

## Phase 4: User Story 1 — Linked export with shared-definition dedup (Priority: P1) 🎯 MVP (T022–T033)

**Goal**: Export a multi-level IronCAD assembly to a package where the root `.ics` has true external references to child `.ics` files. **Each unique IZElement produces exactly one child `.ics`; multiple occurrences share that file** (dedup is required, not optional).

**Independent Test**: A scene with 1 root + 1 part definition placed at 3 positions is exported. `cad/` contains exactly 2 `.ics` files (root + 1 child). All 3 occurrences reference the same file path. Editing any occurrence through the root and saving updates the single shared child file's SHA256.

**Dependencies**: Phase 2 complete and gates passed. Phase 3 tests written (failing).

### Result DTO

`IronCadExportResult` is a simple result DTO with no grouping logic (that lives in `IronCadDefinitionFileMapBuilder`):

```csharp
// IronCAD layer only — no COM types leak into Workspace
public sealed class IronCadExportResult
{
    public string RootFilePath { get; init; }
    public IDictionary<PdmSourceNode, string> SourceNodeToDefFileMap { get; init; }
}
```

### Implementation

- [x] T022 [US1] Create `ElementId` struct in `src/IdeaCadConnector.IronCAD/NormalizeExport/ElementId.cs` — opaque value-type identity token (`int`-backed, `IEquatable<ElementId>`). Add `IDictionary<PdmSourceNode, ElementId> ElementIds` to `IronCadSceneSnapshot` (keep existing `IDictionary<PdmSourceNode, IZElement> Elements` for COM operations unchanged).  **Modify `IronCadSceneNormalizationReader.ReadElement()`**: at the `snapshot.Elements[node] = element` assignment (line 56), assign `ElementIds[node]` by checking a local `Dictionary<IZElement, int>` constructed with **`ReferenceComparer<IZElement>.Instance`** as the equality comparer — new IZElement gets `new ElementId(++counter)`, repeated IZElement gets the existing id. Do NOT use default `Dictionary` equality (COM interop types may not implement `Equals`/`GetHashCode` correctly). Shared definitions now carry the same `ElementId` across occurrences.
- [x] T023 [US1] Create `IronCadDefinitionFileMapBuilder` class in `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadDefinitionFileMapBuilder.cs` — receives `IReadOnlyDictionary<PdmSourceNode, ElementId>` (from `snapshot.ElementIds`) and `PdmNormalizationPlan`, groups `PdmSourceNode` by shared `ElementId`, returns `IDictionary<PdmSourceNode, string>`. Include missing-identity and ambiguity reporting.
- [x] T024 [US1] Create `IronCadExportResult` class in `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadExportResult.cs` with `RootFilePath` (string) and `SourceNodeToDefFileMap` (`IDictionary<PdmSourceNode, string>`)
- [x] T025 [US1] In `IronCadSceneNormalizationWriter.Export()`: build the definition-file map, stage the canonical root in package `cad/`, verify every native-emitted expected definition file, and return `IronCadExportResult` `[IronCAD runtime]`
- [x] T026 [US1] Implement true root external linking through deep module `IronCadNativeSaveAllExternalInvoker.Execute(cadDirectory)`, which invokes IronCAD 2025's native command ID 53046 and completes its folder dialog. Reject scene reconstruction for production because it lost DEMO geometry `[IronCAD runtime]`
- [x] T027 [US1] Temporarily apply canonical filename stems before native externalization, then restore approved scene names and all six PDM properties, update the scene, and save with `Z_LINKS_SAVE_ALL` `[IronCAD runtime]`
- [x] T028 [US1] In `IronCadNormalizeExportCommand` in `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadNormalizeExportCommand.cs`: After `Export()` returns `IronCadExportResult`, pass `SourceNodeToDefFileMap` to `PdmManifestV2Factory.Create(plan, sourceNodeToDefFileMap)` so manifest occurrence entries include `definitionFile`, definitions are deduplicated, and `BomV2` is consistent
- [x] T029 [US1] **MODIFY** existing `IronCadExternalReferenceRecord` in `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadExportPackageVerifier.cs` (lines 16–25). The type already exists with fields `{OccurrencePath, ReportedLinkPath, ResolvedTargetPath, Exists, InsidePackage, PointsToSource, CanonicalFileNameMatch}`. The split design reuses the same DTO for both raw Reader output and enriched Validator output:
  - **Reader sets**: `OccurrencePath` (from scene path), `ReportedLinkPath` (from `ModelLinkPath` / `GetExternallyLinkedInfo`)
  - **Validator sets**: `ResolvedTargetPath`, `Exists`, `InsidePackage`, `PointsToSource`, `CanonicalFileNameMatch` (computed via `PdmExternalReferencePolicy.Evaluate()`)
  - Keep all existing callers working by setting only the fields they own. Deprecate direct construction outside the reader/validator flow.
  - Extract to its own file (`IronCadExternalReferenceRecord.cs`) for clarity if the type will be referenced from multiple compilation units.
- [x] T030 [US1] Create `IronCadExternalReferenceReader` class in `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadExternalReferenceReader.cs` — **raw-only COM adapter**: takes `IZSceneDoc`, traverses occurrences via `GetChildrenZArray()`, reads `ModelLinkPath` on each `IZSceneElement`. **Does NOT check file existence, resolve paths, or compare against the plan** — those are the Validator's job. Returns one record per occurrence, including the root and unlinked children; unlinked records have null/empty `ReportedLinkPath`. Single public method: `Read(IZSceneDoc)`. `[COM automation]` — requires IronCAD runtime and MUST execute on IronCAD's STA thread; no cross-apartment COM marshaling is permitted.
- [x] T031 [US1] Create `IronCadExternalReferenceValidator` class in `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadExternalReferenceValidator.cs` — pure logic, no COM dependencies. Tested with `[xUnit+fakes]`. Receives `IReadOnlyList<IronCadExternalReferenceRecord>` + `PdmNormalizationPlan` + **`IronCadExternalReferenceValidationContext`** (containing `DocumentDirectory`, `PackageRoot`, `CadRoot`, `SourceRoot`, `StagingRoot`). For each record:
  - Calls `PdmExternalReferencePolicy.Evaluate(ReportedLinkPath, context.DocumentDirectory, context.CadRoot, context.SourceRoot, context.StagingRoot, expectedCanonicalName)` — the policy resolves the raw link relative to `DocumentDirectory`, returns `ResolvedTargetPath`, and applies `CadRoot` as the package isolation root
  - **Exact occurrence-set validation**: exclude the root occurrence (`plan.Root` / path `"0"`) from the external-link expected set; every non-root plan child OccurrencePath must have exactly one matching record (missing → fail); every record's non-root OccurrencePath must exist in the plan (unexpected → fail); no duplicate OccurrencePath values (duplicate → fail). A root record with a null/empty link is allowed and is not treated as a missing child link.
- [x] T032 [US1] Add `VerifyExternalLinks(IZSceneDoc openedRoot, PdmNormalizationPlan plan, IronCadExternalReferenceValidationContext context)` to `IronCadExportPackageVerifier` in `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadExportPackageVerifier.cs` — orchestrates: calls `IronCadExternalReferenceReader.Read(doc)` to get raw records, then `IronCadExternalReferenceValidator.Validate(records, plan, context)` to enrich and validate. The command builds the `context` from its package/output paths before calling. Operates on an already-open document (the command owns open/close). Returns validation result. `[COM automation]`
- [x] T033 [US1] Create `IIronCadSceneDocumentService` interface in `src/IdeaCadConnector.IronCAD/NormalizeExport/IIronCadSceneDocumentService.cs` — injectable seam wrapping the current `IronCadAddin` / `IronCadDocumentActivationVerifier` flow:
  ```csharp
  public interface IIronCadSceneDocumentService : IDisposable
  {
      /// <summary>Opens or activates a scene document. Returns the opened IZSceneDoc ready for inspection.
      /// MUST throw on failure so the command can report a clean error without a dangling COM reference.</summary>
      IZSceneDoc OpenDocument(string filePath);
      /// <summary>Closes the document without saving. MUST restore the previously active IronCAD document
      /// (or the empty scene state) that was active before OpenDocument().</summary>
      void CloseDocument();
  }
  ```
  Update `IronCadNormalizeExportCommand` to receive `IIronCadSceneDocumentService` via constructor. Wire lifecycle: `OpenDocument(path)` → pass doc to `VerifyExternalLinks()` → `CloseDocument()` in `finally` when a document was opened → `Dispose()` in the outer cleanup path for COM cleanup on success or failure. `[COM automation]`

**Checkpoint**: T022–T033 complete. The selected writer uses native externalization and the accepted DEMO package contains one canonical root plus 87 canonical external definition files. Remaining acceptance tasks are listed in Phases 5–6.

---

## Phase 5: User Story 3 — Hard-block source external dependencies (Priority: P2)

**Goal**: The existing `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` guard continues to work with the new writer. Any source scene with pre-existing external links blocks export with a clear error.

**Independent Test**: A source scene with any external link (valid file within the same project) is rejected at dependency-discovery stage with `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` identifying the occurrence and link path.

**Dependencies**: US1 complete (writer changes could have broken the guard).

- [ ] T034 [US3] Add unit test in `tests/IdeaCadConnector.Tests/PdmIronCadAdapterTests.cs` verifying `IronCadNormalizeExportCommand` still invokes `IronCadDependencyDiscovery` before `Export()` — guard fires for a scene with external links `[xUnit+fakes]`
- [ ] T035 [US3] Add integration test verifying `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` error code and message identifies the failing component and its link path (uses existing IronCAD adapter test infrastructure) `[IronCAD runtime]`

**Checkpoint**: US3 complete — external dependency guard confirmed.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Manual UAT, documentation, final verification.

- [ ] T036 [P] Run manual UAT per `specs/002-ironcad-linked-export/quickstart.md` — capture SHA256 before/after evidence for FR-013 `[IronCAD runtime]`
- [x] T037 [P] Update `specs/002-ironcad-linked-export/quickstart.md` verification checklist and preserve unchecked items for UAT/performance work not yet evidenced
- [x] T038 Run `dotnet build IdeaCadConnector.sln` and `dotnet test IdeaCadConnector.sln` — 0 warnings/errors; 674 passed, 0 failed/skipped; exact results recorded in baseline evidence
- [ ] T040 [US1] Run broken-child runtime validation for SC-003: delete one child `.ics`, open the exported root on the IronCAD STA thread, verify a broken-link indicator is reported for that occurrence, and verify there is no crash or silent data loss `[IronCAD runtime]`
- [ ] T041 [US1] Run round-trip performance validation for SC-004 with 50 child components; measure from document open through verifier completion, record elapsed time and fail if it is 5 seconds or more `[IronCAD runtime]`
- [ ] T042 [US3] Run external-dependency blocking performance validation for SC-005 with a resolvable and a dangling source link; measure from command start to block, verify each completes within 2 seconds, and record component name/link path in the error `[IronCAD runtime]`
- [ ] T039 Verify `git status --short` — confirm only intended files changed; compare with baseline evidence from T001

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup
  └── No dependencies
Phase 2: Foundational (Phase 0)
  └── Depends on Phase 1
      └── [G1/T3, G2/T2] — If blocked → ALL CANCELLED after T010
Phase 3: TDD (failing tests)
  └── Depends on Phase 2 (not blocked) — tests reference new types
Phase 4: US1 — Linked export with dedup (P1)
  └── Depends on Phase 3 (tests exist first)
Phase 5: US3 — External dependency block (P2)
  └── Depends on Phase 4 (writer pipeline exists)
Phase 6: Polish
  └── Depends on all desired user stories complete
```

### Test Harness Dependencies

| Harness | When to use | Requirements |
|---------|-------------|--------------|
| `[xUnit+fakes]` | T014–T021, T034 | `dotnet test` only; no IronCAD |
| `[IronCAD runtime]` | T004–T009 (Phase 0), T025–T027 (writer), T035 (US3 integration), T036 (UAT), T040 (broken-link), T041 (performance), T042 (blocking timing) | Requires `IRONCAD.exe` installed |
| `[COM automation]` | T003 script (drives T004–T009), T030 (reader), T032 (verifier orchestration), T033 (command wiring + seam) | Runs via `interop.ICApiIronCAD` in-process with IronCAD |

### Parallel Opportunities

- **Phase 3**: T014–T021 all `[xUnit+fakes]` — can run in parallel via `dotnet test`.
- **Phase 4**: T022 (ElementId + reader identity assignment) and T024 (export result DTO) can run in parallel. T023 depends on T022; T025 depends on T022–T024; T026–T028 follow the writer/map flow; T029 → T030 → T031 → T032 are sequential (DTO → Reader → Validator → Verifier); T033 depends on T032 and the command lifecycle.
- **Phase 6**: T036, T037, T040, and T041 can run in parallel when they use isolated temporary workspaces. T042 depends on the US3 implementation/tests. T038 (build) must precede T039 (status check).

### Within Each Phase

- Phase 2: Sequential read-only tests before production code
- Phase 3: All tests fail before any implementation
- Phase 4: Green-phase — make tests pass

---

## Implementation Strategy

### MVP (US1 Only = Phase 4)

1. Phase 1: Setup
2. Phase 2: Phase 0 research (MANDATORY gate — if blocked, STOP)
3. Phase 3: Write all 8 failing tests
4. Phase 4: Implement linked export with dedup → make all tests pass
5. Validate: Manual UAT per quickstart.md → FR-013 evidence
6. MVP complete: Linked export with shared-definition dedup, deduplicated manifest (`Definitions[]`, `Occurrences[]`, `BomV2`), package validation.

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready (or blocked)
2. Phase 3 + Phase 4 → MVP: Linked export with dedup (includes round-trip verification)
3. Add US3 (P2) → External dependency guard confirmed

---

## Notes

- [P] tasks = different files, no dependencies
- [US1], [US3] labels map tasks to user stories (US2 and US4 merged into US1)
- All Phase 3 tests MUST fail before Phase 4 implementation starts (red → green)
- Run `dotnet build` and `dotnet test` after each logical group
- If Phase 0 gates fail (G1/G2), no production source changes are made — only `research.md` records the block decision
- Baseline evidence preserves pre-existing dirty-tree state; do not `git stash` unrelated changes
- Test harness labels must be checked before running: `[xUnit+fakes]` can run anytime; `[IronCAD runtime]` and `[COM automation]` require IronCAD to be installed and registered
