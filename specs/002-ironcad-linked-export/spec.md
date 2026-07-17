# Feature Specification: IronCAD Linked Normalized Export

**Created**: 2026-07-16

**Status**: Runtime-approved implementation; remaining UAT gates are tracked in `tasks.md`

**Input**: Cải thiện chức năng chuẩn hóa và xuất PDM cho IronCAD — export Scene có Assembly/Part con, file root giữ external link thật tới file CAD con.

## Remaining Validation Risks

- Shared-definition deduplication still needs the dedicated three-occurrence runtime fixture (T006).
- Save-through-root still needs recorded SHA256 before/after evidence (T007/T036/FR-013).
- Broken-link behavior and the two performance criteria remain unverified (T040–T042).
- Native dialog automation is confirmed on the current English IronCAD 2025 runtime; localized dialog captions have not been validated.

## Clarifications

### Session 2026-07-16

- Q: How should the export writer convert embedded child occurrences into external file links? → A: Stage the approved root in package `cad/`, temporarily rename definitions to canonical filename stems, invoke IronCAD 2025's native `Save All As External` operation for that directory, restore approved names/properties, then save all links.
- Q: Does the preview dialog need modification for linked export? → A: No changes needed (Option A).
- Q: Does ICAPI provide a public one-step embedded-to-external conversion API? → A: No. The accepted implementation invokes the same native IronCAD command used by the ribbon. `RunCommand((eZCommand)53046)` is invalid because 53046 is a native command/resource ID, not a public `eZCommand` value.
- Q: How is shared definition identity determined? → A: `object.ReferenceEquals(element1, element2)` on `IZElement` — pattern already proven across 4 traversals in the codebase. Valid only within the same active IronCAD session; does not persist across save/reopen.
- Q: Should the feature support source scenes with pre-existing external dependencies? → A: No — keep the existing hard block in `IronCadNormalizeExportCommand` (`BLOCKED_SOURCE_DEPENDENCY_ISOLATION`). Out of scope for this feature.
- Q: Why not rebuild with `Shapes.Add(filePath)` / `ImportFile(filePath, true)`? → A: Those calls create useful synthetic linked fixtures, but rebuilding the real `DEMO.ics` produced linked tree shells with missing/blank geometry. They are rejected for the production writer.

## User Scenarios & Testing

### User Story 1 — Export a multi-level assembly with linked child files (Priority: P1)

A mechanical designer works on an IronCAD assembly scene that contains sub-assemblies and parts arranged in a tree. When running "Chuẩn hóa & Xuất PDM", the exported package must contain a root `.ics` file whose occurrences are linked via true external references to separate child `.ics` files rather than embedded inline. The designer must be able to open the root file, modify any child component, save the root, and have changes persist to the corresponding child `.ics` file on disk.

**Why this priority**: This is the core behavior the feature describes. Without it the exported package cannot support native CAD linking, which defeats the purpose of the PDM workflow.

**Independent Test**: An assembly scene with 2 child parts is exported. The resulting root `.ics` is opened standalone in IronCAD — each child occurrence must show as an external reference to a file in the `cad/` subfolder. Modifying a child through the root and saving must update the child file's content (verified by SHA256 change in the child file).

**Acceptance Scenarios**:

1. **Given** a source IronCAD scene with root assembly (`ProjectCode=MYASM`, itemCode=`ROOT`, displayName=`Main`), sub-assembly (itemCode=`A01`, displayName=`Sub1`), and part (itemCode=`P01`, displayName=`Bracket`), **When** the user runs normalize and export with default plan, **Then** the exported package contains `cad/MYASM__ROOT__Main.ics`, `cad/MYASM__A01__Sub1.ics`, and `cad/MYASM__P01__Bracket.ics` and the root scene's occurrence for Sub1 references `cad/MYASM__A01__Sub1.ics` (not an embedded copy).

2. **Given** an exported package from scenario 1, **When** the user opens `MYASM__ROOT__Main.ics` in IronCAD, edits the Bracket child, and saves, **Then** `cad/MYASM__P01__Bracket.ics` on disk is updated (its SHA256 changes) while the root file is also updated.

3. **Given** an exported package from scenario 1, **When** the user opens `MYASM__ROOT__Main.ics` and inspects each occurrence's ModelLinkPath, **Then** no link points to a path outside the package `cad/` directory.

---

### User Story 2 — Shared definition for multi-occurrence components (Priority: P2)

A component definition is used by multiple occurrences in the same scene (e.g., the same bolt part appears 4 times). When exported, all occurrences must reference the same single child `.ics` file rather than creating 4 separate files.

**Why this priority**: This ensures BOM accuracy and storage efficiency. Multiple files for the same definition would break the round-trip and manifest consistency.

**Independent Test**: A scene with one part definition placed as 3 occurrences is exported. The `cad/` folder must contain exactly 1 child `.ics` for that part, and all 3 occurrences in the root must reference the same file.

**Acceptance Scenarios**:

1. **Given** an IronCAD scene where `Bolt` part is placed at 3 different locations (itemCode=`P01`, displayName=`Bolt`), **When** exported, **Then** `cad/` contains exactly one `<PROJECT>__P01__Bolt.ics` file, and all 3 occurrences in the root scene reference that same file path.

2. **Given** the exported package from scenario 1, **When** the user opens the root file in IronCAD, **Then** editing any one of the 3 bolt occurrences and saving updates the shared `<PROJECT>__P01__Bolt.ics` file, and the change is visible in all 3 occurrences upon reload.

---

### User Story 3 — Hard-block export for any source external dependency (Priority: P2)

The current `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` guard rejects any source scene that contains pre-existing external links (whether resolvable or dangling). This feature inherits that guard unchanged: all external dependencies block export with an error identifying the component and its external link path. Enabling import of external dependencies is out of scope.

**Why this priority**: The feature changes how the writer creates external links in the exported package, not how the source scene's pre-existing links are handled. Keeping the hard block prevents regressions in existing behavior.

**Independent Test**: A source scene with any external link (pointing to a valid file within the same project) is rejected at the dependency-discovery stage.

**Acceptance Scenarios**:

1. **Given** a source IronCAD scene containing an occurrence whose external link target is a valid file within the source directory, **When** the user runs normalize and export, **Then** the operation fails with `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` identifying the occurrence and its link path.

2. **Given** a source IronCAD scene containing an occurrence with a dangling external link, **When** the user runs normalize and export, **Then** the operation fails with `BLOCKED_SOURCE_DEPENDENCY_ISOLATION` identifying the occurrence and the missing file path.

---

### User Story 4 — Round-trip verification of exported package (Priority: P3)

After export, the system automatically opens the exported root file, re-reads the structure, and verifies that it matches the approved normalization plan — including external link paths, file names, and occurrence paths.

**Why this priority**: Round-trip verification catches corruption or misconfiguration before the package is used for push or distribution.

**Independent Test**: An export that deliberately introduces a link mismatch (e.g., pointing to wrong child filename) is detected by the verifier and reported as a failure.

**Acceptance Scenarios**:

1. **Given** a completed export, **When** the system performs automatic round-trip verification, **Then** every external link in the exported root scene is checked against the plan's canonical file names and occurrence paths, and any mismatch causes the export to fail with a detailed report.

### Edge Cases

- What happens when a child component is an external reference to a file that was already moved or renamed in the source workspace? The system must detect this during dependency discovery and block the export.
- What happens when the same .ics definition is referenced by both a direct occurrence and as a nested child? The shared-definition logic must treat occurrences by their underlying element identity, not by scene tree position.
- How does the system handle a scene where all children are already embedded (no external links in source)? The export should still produce separate child .ics files and create new external links in the root — it should not require pre-existing external links.
- What if the user cancels the export mid-way? Temporary staging files must be cleaned up, and the source files must remain unchanged.
- What if a child file name in the plan collides with another child's canonical name? The planner's duplicate-detection warning must fire, and the user must resolve the collision in the preview dialog before proceeding.
- How does the manifest represent occurrences vs definitions? The manifest MUST distinguish occurrence identity (per-occurrence `nodeId`, `occurrencePath`, `parentNodeId`) from definition identity (which definition `.ics` file the occurrence references). Two occurrences sharing the same definition file MUST each have their own manifest entry with the same canonical file reference.

## Requirements

### Functional Requirements

- **FR-001**: The export writer MUST produce the same hierarchy, geometry, transforms, and true external-file relationships as IronCAD 2025's `Assembly > Save All As External` operation, while applying the approved canonical PDM filenames and metadata. Implementations that rebuild an empty scene, copy only the scene tree, or leave child geometry embedded do not satisfy this requirement.
- **FR-002**: Each unique component definition (element identity) in the scene tree MUST produce exactly one child `.ics` file in the package `cad/` directory, regardless of how many occurrences use it.
- **FR-003**: Every occurrence of the same component definition MUST reference the same child `.ics` file path in the exported root scene.
- **FR-004**: The root scene file MUST NOT contain embedded copies of child component data that would prevent saving changes back to child files.
- **FR-005**: All external links in the exported package MUST point to paths inside the package `cad/` directory. No link may reference the original source directory, staging directory, or any external path.
- **FR-006**: Before export, the system MUST block the operation if the source scene contains ANY external dependency (as currently enforced by `IronCadNormalizeExportCommand.cs:55-57`: `BLOCKED_SOURCE_DEPENDENCY_ISOLATION`). This hard block covers both resolvable and unresolvable external references. The error MUST identify the failing component. This feature will NOT lift this block — external dependency support is out of scope.
- **FR-007**: After writing the package, the system MUST perform a round-trip verification by opening the exported root scene, re-reading its external link structure, and confirming every link matches the approved normalization plan (occurrence path, canonical file name, target existence).
- **FR-008**: The exported package structure MUST match the normalization plan's tree: the root node at `cad/<root-canonical-name>.ics`, each child at `cad/<child-canonical-name>.ics`, and the `pdm-bom-manifest.json` must be consistent with the actual file tree.
- **FR-009**: If the round-trip verification finds any mismatch (missing child file, wrong link target, extra file), the export MUST be recorded as failed, and the partial package MUST be cleaned up.
- **FR-010**: The export MUST preserve the PDM custom properties (NodeId, ItemCode, ItemType, DisplayName, ProjectCode, Revision) on all elements in the exported files, identical to the current behavior.
- **FR-011**: The export MUST NOT modify Aras schema, remote checkout protocol, or any code outside the IronCAD and Workspace normalize/export layer.
- **FR-012 (Phase 0 Research)**: Before finalizing the export writer, runtime validation MUST compare the result with IronCAD 2025's native `Save All As External` output and cover hierarchy preservation, transform preservation, shared occurrence deduplication, save-through-root, custom property round-trip, and external link isolation. The selected native-equivalent approach and rejected alternatives MUST be recorded in `research.md` and `plan.md`.
- **FR-013 (UAT Validation)**: As a mandatory acceptance gate, the implementation MUST include a manual UAT test proving that editing a child component through the exported root scene and saving the root updates the corresponding child `.ics` file on disk. Test evidence (SHA256 before/after, IronCAD save confirmation) MUST be captured and recorded.

### Key Entities

- **Normalization Plan (PdmNormalizationPlan)**: The approved mapping from source scene elements to canonical PDM file names, item codes, and occurrence paths. Already exists; the linked-export writer consumes this plan unchanged.
- **IZElement (COM interop object)**: The in-memory IronCAD scene element (`IZPart`, `IZAssembly`, `IZSceneElement`). Identity by `object.ReferenceEquals` is a **hypothesis pending T3 runtime validation** — export deduplication groups occurrences by IZElement reference. If T3 reveals that IronCAD creates distinct IZElement instances per occurrence (even for what the user considers "the same definition"), linked export **MUST be blocked with a clear error** — no fallback to `ItemCode` equivalence, because that could silently produce incorrect deduplication (different parts with the same code merged into one file). Valid only within a single active IronCAD session; does NOT survive save/close/reopen.
- **Occurrence**: A specific position in the scene tree tracked by `occurrencePath`. Each occurrence maps to exactly one `IZElement` (its definition). Multiple occurrences may share the same `IZElement` (shared definition). Each occurrence entry in the plan/manifest has a unique `NodeId` and `ParentNodeId`.
- **Definition File**: A standalone `.ics` file produced by IronCAD's native externalization operation. One unique `IZElement` → exactly one definition file. Multiple occurrences sharing that `IZElement` all reference the same definition file in the exported root scene.
- **Cardinality rule**: N occurrences → M IZElements (M ≤ N) → M definition files. The manifest MUST record both occurrence entries (for tree structure) and definition file entries (for physical file mapping) — see `data-model.md` for the finalized relationship diagram.
- **External Link Record**: A data object recording the occurrence path, link target path, resolved target path, and whether the target exists inside the package. Used by the round-trip verifier.
- **Exported Package**: A directory containing `cad/` (`.ics` files) and `pdm-bom-manifest.json`. The `cad/` subdirectory holds the root scene file plus one `.ics` per unique child definition, with the root's external links pointing to those child files.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A source scene with 1 root assembly and 5 unique child parts (no shared definitions) produces exactly 6 `.ics` files in the package `cad/` directory after export.
- **SC-002**: A source scene with 1 root assembly, 1 unique child part placed as 3 occurrences, produces exactly 2 `.ics` files in `cad/` (root + 1 child). All 3 occurrences reference the same child file path.
- **SC-003**: When any child `.ics` in the exported package is deleted before opening the root, the root scene opens in IronCAD with a broken-link indicator for that occurrence (no crash or silent data loss).
- **SC-004**: Round-trip verification completes in under 5 seconds for a scene with up to 50 child components.
- **SC-005**: If a source scene has any external dependency (resolvable or dangling), the export is blocked within 2 seconds and the error message contains the component's name and link path.
- **SC-006**: All existing normalize/export tests (PdmNormalizationTests, PdmNormalizeExportSafetyTests, PdmIronCadAdapterTests) continue to pass with no regressions.
- **SC-007**: A mandatory UAT test proves that editing a child component through the exported root scene, saving the root, and then verifying the child `.ics` file's SHA256 has changed — documented with before/after evidence.

## Assumptions

- **Native externalization confirmed**: IronCAD 2025's `Save All As External` operation preserves the existing scene and emits the complete linked hierarchy into one selected directory. The runtime-approved writer invokes this native operation on a staged scene after temporarily applying canonical filename stems.
- **Rejected reconstruction approach**: `Pages.Add()` plus `Shapes.Add()` / `ImportFile()` can create external occurrences for synthetic fixtures, but rebuilding the production `DEMO.ics` scene this way produced tree shells with missing/blank geometry and is not used by the normalize/export pipeline.
- **ICAPI confirmed — read-only link APIs**: `ModelLinkPath` (read) and `GetExternallyLinkedInfo(out bool)` (read) work correctly and are used throughout the existing codebase.
- **No public one-call ICAPI equivalent**: `IZBaseApp.RunCommand((eZCommand)53046)` was runtime-tested and rejected with `Invalid input arguments`; resource ID `53046` is a native ribbon/WM_COMMAND identifier, not a valid public `eZCommand` value.
- **Definition identity — HYPOTHESIS pending T3**: `object.ReferenceEquals(IZElement, IZElement)` is the candidate identity method (used in 4 existing traversals for cycle detection). Whether it correctly identifies shared definitions across multiple occurrences is **not yet confirmed** — T3 in the Phase 0 test suite must validate this. **If T3 fails, linked export is blocked with a clear error** — no fallback to `ItemCode` or `CanonicalFileName` equivalence, because those are not reliable definition identity mechanisms and could silently produce incorrect deduplication. The feature does NOT degrade to one-file-per-occurrence. `IZElement.Id` (int) is diagnostic-only, NOT verified for definition identity. The cardinality model remains: N occurrences → M unique definitions → M definition .ics files, but the definition identity mechanism is subject to T3 results.
- **Transform preservation — HARD GATE pending T2**: The accepted native DEMO output visibly preserved the design, but numeric position/rotation/scale comparison is still required. Any mismatch blocks release; the feature MUST NOT silently produce misplaced components (see `plan.md` G2).
- **External dependency blocking confirmed**: `IronCadNormalizeExportCommand.cs:55-57` hard-blocks any scene with pre-existing external dependencies (`BLOCKED_SOURCE_DEPENDENCY_ISOLATION`). This feature will NOT lift this block.
- **Writer behavior**: The writer saves the staged root into package `cad/`, temporarily renames definitions to canonical filename stems, invokes the native externalization handler with `cad/` as its folder selection, restores approved scene names/properties, and saves all links.
- The current preview dialog, naming policy, and output safety validators remain unchanged. The change is isolated to the export writer and round-trip verifier.
- The user always saves the source scene before running export (already enforced by current code).
