# Phase 0 Research: IronCAD Linked Normalized Export

**Date**: 2026-07-16; final runtime decision 2026-07-17
**Feature**: [spec.md](spec.md)

## Final Runtime Decision — Native Save All As External

The production writer uses IronCAD 2025's native `Assembly > Save All As External` behavior. It does not rebuild the scene and does not attempt to set `ModelLinkPath` directly.

Selected flow:

1. Build the occurrence-to-definition-file map from the approved normalization plan.
2. Stage the root as `cad/<canonical-root>.ics` with `Z_LINKS_IGNORE`.
3. Temporarily set each definition name to its canonical filename stem.
4. Invoke native command ID `53046` through `WM_COMMAND`; select package `cad/` in IronCAD's folder dialog.
5. Verify every expected definition `.ics` exists.
6. Restore approved scene names and all six PDM properties, update the scene, and save using `Z_LINKS_SAVE_ALL`.

The native interaction is hidden behind the deep module `IronCadNativeSaveAllExternalInvoker.Execute(destinationDirectory)`. The writer only supplies the destination; window discovery, modal dialog selection, timeout handling, and native message dispatch remain local to that module.

### Accepted DEMO evidence

- Source: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\Demo\DEMO.ics`
- Manually generated native reference: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\DEMO-PDM-Export` (87 externalized definition `.ics` files).
- Accepted plugin package: `C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\DEMO-PDM-Export\DEMO`.
- Accepted package `cad/`: 88 `.ics` files total (one canonical root plus 87 canonical definition files), 30,359,552 bytes total.
- Root: `cad\DEMO__ROOT__DEMO.ics`, 2,371,584 bytes, written 2026-07-17 15:12:32 local time.
- Manifest: `pdm-bom-manifest.json`, 67,690 bytes, written at the same time.
- Writer log reached `NATIVE_COMMAND_COMPLETED` and `NATIVE_SAVED_ALL`; package validation retained the output.
- User runtime acceptance: exported root displayed the original geometry and true external-link entries after reopening.

This evidence resolves the practical hierarchy/geometry/externalization route for the production `DEMO.ics`. It does not replace the still-open SHA256 edit-through-root, dedicated shared-occurrence, broken-link, or performance tests.

### Rejected approaches

- `Pages.Add()` plus `Shapes.Add()` / `ImportFile()` reconstruction: created external links for synthetic fixtures but produced tree shells and blank/missing geometry for `DEMO.ics`.
- `IZAssembly.SaveAs()` / `IZPart.SaveAs()` followed by reopen/relink: caused document locks and share violations and did not establish the required root relationship reliably.
- `SaveAsCopy`/`SaveAs` alone with link options: preserved or embedded content but did not perform native embedded-to-external conversion.
- `IZBaseApp.RunCommand((eZCommand)53046)`: failed with `Invalid input arguments`; command ID 53046 is not a public `eZCommand` value.
- External COM/ROT probe automation: unstable and could hang or terminate IronCAD; it is not part of the production path.

The older Outcome A/Outcome B notes below are retained as investigation history and are superseded by this final decision.

## Research Tasks

| # | Unknown | Source | Resolution |
|---|---------|--------|------------|
| R1 | Does staging with `Z_LINKS_IGNORE` plus native externalization preserve the production tree/geometry? | FR-012 | ✅ Accepted DEMO runtime reopened with hierarchy, geometry, and true external links. |
| R2 | Is there a public ICAPI relink call? | Assumptions | ❌ No; `RunCommand((eZCommand)53046)` is invalid. Native `WM_COMMAND` is used behind the invoker module. |
| R3 | Can `Shapes.Add(filePath)` / `ImportFile()` create links? | Assumptions | ✅ For synthetic fixtures, but ❌ rejected for production DEMO reconstruction due to blank/missing geometry. |
| R4 | Can native externalization produce canonical standalone definitions? | Assumptions | ✅ 87 canonical definition files plus one canonical root in the accepted package. |
| R5 | Does `object.ReferenceEquals(IZElement, IZElement)` correctly identify shared definitions across multiple occurrences? | Spec Key Entities | ⚠️ **HYPOTHESIS — requires T3 validation** |
| R6 | Does `ImportFile()` preserve the original occurrence local transform, or does it place at origin? | data-model.md | ⚠️ **HYPOTHESIS — requires T2 validation** |

## Runtime Attempt — Demo.ics (2026-07-17)

Source used:

`C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\Demo\DEMO.ics`

Attempted Outcome B validation with an STA C# harness:

1. Create a new IronCAD scene through `IronCAD.Application`.
2. Add `DEMO.ics` with `Shapes.Add()` and fall back to `ImportFile()`.
3. Save the new root using `Z_LINKS_IGNORE`.
4. Read the added element's `ModelLinkPath`.

Result: **BLOCKED_RUNTIME_AUTOMATION**. IronCAD COM stopped responding during scene creation/import before the harness could produce a root file or a link observation. The harness timed out after 60 seconds and its two spawned IronCAD processes were terminated. No T1–T6 pass is claimed from this attempt.

Historical decision at that point: keep T026/T027 deferred. This was superseded by the successful in-process native-command implementation documented above.

## Fixture Evidence — IRONCASE

Fixture:

`C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\IRONCASE`

The fixture contains one root assembly (`Assembly-IRONCASE-Ver1.0A.ics`) and six child `.ics` files. Its structure manifest describes the root-to-child hierarchy. The repository generator that produced this fixture is `tools/CreateIronCadTestFiles/Program.cs` and uses the following concrete workflow:

1. Create a new scene with `icApp.Pages.Add(Type.Missing, Type.Missing)`.
2. Add each child with `asmPage.Shapes.Add(detailPath)`.
3. Fall back to `asmPage.ImportFile(detailPath, true)` when `Shapes.Add` fails.
4. Save child and root scenes with `SaveAs(..., Z_LINKS_IGNORE, true)`.

This is the strongest available evidence for Outcome B. It does not prove the edit/save round-trip yet, but it establishes the ICAPI workflow to implement and validate inside the IronCAD add-in rather than through the hanging external COM harness.

---

## R1 — SaveAsCopy with Z_LINKS_IGNORE tree preservation

**Status**: ⚠️ **Cannot be resolved by static code analysis. Requires runtime test in IronCAD.**

### Decision Path

Two possible outcomes, determined by running the Phase 0 validation script inside IronCAD:

#### Outcome A (Preferred): Tree preserved

`SaveAsCopy(path, Z_LINKS_IGNORE, true)` produces a root `.ics` whose scene tree, element structure, occurrence paths, and PDM custom properties are intact. Children are unlinked (no embedded data, no external reference).

**If this passes**:
- Writer uses `SaveAsCopy` with `Z_LINKS_IGNORE` for the root
- Each child already saved via `IZPart.SaveAs()` / `IZAssembly.SaveAs()`
- After root save, iterate all child occurrences via `GetChildrenZArray()`, for each:
  - Read its position/orientation (implicitly preserved by scene tree)
  - Set `ModelLinkPath` or use `IZSceneElement` API to point to the child `.ics` file
- Apply custom properties on root scene elements (from snapshot)
- **Simpler implementation, no scene rebuild needed**

#### Outcome B (Fallback): Tree dropped

`SaveAsCopy(path, Z_LINKS_IGNORE, true)` produces a root `.ics` with only the root element (top-level scene data, no children).

**If this happens**:
- Writer must **rebuild the root scene from scratch**:
  1. Create new empty scene via `app.NewScene()` (or equivalent)
  2. For each child definition (by reference-equality group), call `sceneDoc.ImportFile(childIcsPath, true)` or `sceneDoc.Shapes.Add(childIcsPath)` to add as external link
  3. Set PDM custom properties on each imported element (`Apply()`)
  4. Save the rebuilt scene via `sceneDoc.SaveAs(finalRootPath)`
- **More complex: requires mapping original occurrence positions to new elements**

## Runtime Test Suite (Phase 0 — run inside IronCAD)

### Test T1: Hierarchy preservation

**Purpose**: Verify that `SaveAsCopy` with `Z_LINKS_IGNORE` (Outcome A candidate) or `ImportFile` rebuild (Outcome B) preserves the parent-child tree structure.

**Setup**: Open a 3-level hierarchy:
```
Root Assembly
  ├── SubAssembly
  │     ├── Part A
  │     └── Part B
  └── Part C
```

**Procedure**:
1. Run `Export()` with the candidate approach (Outcome A first using `Z_LINKS_IGNORE`; if impossible, Outcome B)
2. Open exported `cad/ROOT.ics` in IronCAD
3. Traverse `GetTopElement().GetChildrenZArray()` recursively
4. Verify depth = 3, child count = 3 at root, 2 under SubAssembly

**Pass criteria**: Exported scene tree matches source hierarchy exactly.

---

### Test T2: Transform preservation

**Purpose**: Determine whether the export approach preserves each occurrence's local 3D transform (position, rotation, scale) relative to its parent. This is an **unknown** — `ImportFile` may place occurrences at origin rather than preserving the original transform.

**Setup**: A root assembly with 3 parts placed at different local positions relative to the root:
- Part A at (0, 0, 0) — origin
- Part B at (100, 50, 0) — translated
- Part C at (0, 0, 200) with rotation

**Procedure**:
1. Record source transforms via IronCAD API (`IZTransform`, `Position`, `Rotation`) for each occurrence
2. Run export with the candidate approach (Outcome A first using `Z_LINKS_IGNORE`)
3. Open exported root `.ics`
4. Read each occurrence's transform and compare with source values

**Pass criteria (Outcome A)**: All transforms match within floating-point tolerance. Origin, translation, and rotation values are identical to source.

**Pass criteria (Outcome B)**: Hierarchy must be correct AND all occurrence transforms must match source within floating-point tolerance. If transforms are lost by `ImportFile()`, this approach is **blocked** — the export MUST fail with a clear error explaining that the selected approach cannot preserve occurrence positions.

**If transform preservation fails for both approaches**: The overall linked export feature is **blocked** until a viable approach is found. The feature cannot silently produce misplaced components.

---

### Test T3: Shared occurrence deduplication (IZElement hypothesis validation)

**Purpose**: **CRITICAL** — validate the hypothesis that `object.ReferenceEquals(IZElement, IZElement)` correctly identifies shared definitions. This test determines whether IZElement-based deduplication is viable.

**Setup**: A scene with 1 root + 1 part definition placed at 3 different positions.

**Procedure**:
1. Capture the 3 `IZElement` references for the bolt occurrences
2. Assert: `object.ReferenceEquals(el1, el2) == true && ReferenceEquals(el1, el3) == true`
3. Run `Export()`
4. Count files in `cad/` directory (excluding root)
5. Open exported root and inspect each occurrence's external link

**Pass criteria**:
- `ReferenceEquals` returns `true` for all 3 occurrences (they share the same underlying COM object)
- Exactly 2 `.ics` files in `cad/` (root + 1 shared child)
- All 3 occurrences in the exported root reference the same definition file

**Failure handling**:
- If `ReferenceEquals` returns `false` for what the user considers "the same part", IZElement-based dedup is NOT viable
- **Linked export MUST be blocked with a clear error**: shared-definition deduplication cannot be guaranteed. No fallback to `ItemCode` or `CanonicalFileName` equivalence — those are not reliable definition identity mechanisms and could silently merge genuinely different parts into one file if they happen to share the same code. The feature does NOT degrade to one-file-per-occurrence.
- Document this finding in the decision table: record that IZElement dedup failed and linked export is blocked.

---

### Test T4: Save-through-root

**Purpose**: Prove that editing a child through the exported root and saving the root updates the child `.ics` file on disk (FR-013).

**Setup**: Exported package from Test T1 with a 3-level hierarchy.

**Procedure**:
1. Record SHA256 of all child `.ics` files
2. Open `cad/ROOT.ics` in IronCAD
3. Select a child occurrence (Part A at depth 2)
4. Modify its geometry (e.g., change a dimension or color)
5. Save the root scene (`File > Save`)
6. Re-compute SHA256 of the child `.ics` for Part A
7. Verify the child SHA256 changed; verify root SHA256 changed
8. Close root, open child `.ics` standalone

**Pass criteria**:
- Child `.ics` SHA256 changed (its definition was updated)
- Root `.ics` SHA256 changed (reference was updated)
- Opening child `.ics` standalone shows the edit
- Other child `.ics` files (unmodified parts) have unchanged SHA256

**Edge case variant**: Apply the same test to a shared definition (Test T3) — edit one occurrence, save root, verify the single shared `.ics` changed, and that all 3 occurrences show the edit on reload.

---

### Test T5: Custom property preservation after round-trip

**Purpose**: Verify that PDM custom properties survive the export → reopen cycle.

**Setup**: Scene with 1 root + 2 children, PDM properties assigned via `Apply()`.

**Procedure**:
1. Export (using the chosen approach)
2. Open exported root `.ics`
3. Re-read PDM properties from root and all child elements via `GetCustomPropManager(1)`

**Pass criteria**: All 6 PDM properties (`NodeId`, `ItemCode`, `ItemType`, `DisplayName`, `ProjectCode`, `Revision`) present and correct on every element.

---

### Test T6: External link isolation

**Purpose**: Verify no external link in exported package points outside `cad/`.

**Setup**: Exported package from any test above.

**Procedure**:
1. Open exported root `.ics`
2. Traverse all occurrences, read `ModelLinkPath` on each `IZSceneElement`
3. For elements where `GetExternallyLinkedInfo(out bool)` returns true, check path

**Pass criteria**: Every external link path resolves to a file under the package's `cad/` directory. No link points to source, staging, or any external path.

---

## R2 — Programmatic relink API

**Status**: ❌ Not confirmed. No `LinkToFile` usage in codebase. No `IZSceneElement.ModelLinkPath` setter found.

### Rationale
- `ModelLinkPath` is used **only as a getter** (read-only) in all 4 traversal files
- `GetExternallyLinkedInfo(out bool)` is also read-only
- No evidence of a documented ICAPI setter for `ModelLinkPath`

### Impact
If Outcome A (tree preserved) is true but there is no programmatic relink setter, the scene tree is preserved but occurrences remain unlinked. This would force Outcome B (rebuild) regardless.

**Phase 0 must also test**: After `SaveAsCopy` with `Z_LINKS_IGNORE`, does ICAPI provide any way to assign an external file to an existing scene element? If not, only Outcome B (rebuild) is viable.

---

## R3 — Shapes.Add / ImportFile for external links

**Status**: ✅ Confirmed by existing tool code.

### Evidence
- `tools/New-IronCadProject.ps1:126-164` — `AddLinkedDocument()` function:
  - Attempts `sceneDoc.Shapes.Add(filePath)` to add a linked external file
  - Fallback: `sceneDoc.ImportFile(filePath, true)`
- `tools/CreateIronCadTestFiles/Program.cs:215-226` — same pattern:
  - `asmPage.Shapes.Add(detailPath)`
  - `asmPage.ImportFile(detailPath, true)` as fallback

Both approaches create an occurrence that is externally linked to the source file. Saving the scene persists the link.

### Impact
Confirmed fallback path for Outcome B. When rebuilding the root scene, each child `.ics` can be added via `ImportFile()` to create an external-linked occurrence. The `IZElement` returned by `ImportFile()` can then receive PDM custom properties via `Apply()`.

---

## R4 — SaveAs preserves custom properties

**Status**: ✅ Confirmed by existing implementation.

### Evidence
- `IronCadSceneNormalizationWriter.cs:44-54` — current `Export()` first calls `Apply()` (writes PDM custom properties), then `SaveAs()` on each child
- The child `.ics` files produced by `SaveAs()` retain the properties when re-opened (verified by round-trip test in `IronCadExportPackageVerifier`)

### Impact
No changes needed for child save behavior. Current `SaveAs()` calls are correct.

---

## Consolidated Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Root/definition save approach | Stage root + native `Save All As External` + restore/save all | Runtime-accepted on production DEMO; preserves geometry and creates true links |
| Native seam | `IronCadNativeSaveAllExternalInvoker.Execute(cadDirectory)` | Hides command dispatch and modal folder handling behind one deep-module interface |
| Scene reconstruction | Rejected | `Shapes.Add()` / `ImportFile()` linked synthetic fixtures but lost production DEMO geometry |
| Shared definition deduplication | ⚠️ **Pending T3** | Hypothesis: `IZElement` reference equality. **If T3 fails, block linked export with error** — no fallback to `ItemCode` equivalence |
| Transform preservation | ⚠️ **Pending numeric T2 — HARD GATE** | DEMO is visually preserved; formal numeric comparison remains required |
| External dependency blocking | Remain blocked | Spec and research both confirm out of scope |
| Preview dialog | No changes | Confirmed in clarification session |
| Manifest schema | `Definitions[]` + `Occurrences[]` (V2 already) | Existing `PdmPackageManifest` already has this structure; only `GetDefinitionId` needs dedup logic change |

## Deferred Questions (to Plan Phase)

| Question | Why Deferred |
|----------|-------------|
| Exact ICAPI calls for scene creation (`NewScene` vs `NewDocument`) | Need IronCAD runtime to verify; determined in implementation phase |
| Relative vs absolute external link paths in root `.ics` | Determined by ICAPI behavior; manifest must record relative paths |
| Manifest schema extension for occurrence/definition separation | `PdmPackageManifest` model review needed; potentially deferred to separate task |
