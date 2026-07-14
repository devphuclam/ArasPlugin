# IDEA PDM StudyCase BOM Reader Design

## Goal

Read the active normalized IronCAD `.ics` scene without modifying it and display its complete BOM in a read-only IDEA PDM viewer.

## Scope

- Add an IronCAD command named `Đọc dữ liệu PDM`.
- Read Scene Root, Assembly, and Part nodes plus the six `PDM.*` custom properties.
- Build a UI-independent snapshot and validation result.
- Display all nodes, including invalid nodes, in `PDM Model Viewer`.
- Verify active path, document `Modified`, and source SHA-256 remain unchanged.
- Do not call Aras, Normalize Export, save APIs, write APIs, rename APIs, or `CloseFile`.

## Architecture

### Workspace model and validation

`IdeaCadConnector.Workspace/PdmModel` owns `PdmModelSnapshot`, `PdmModelNode`, validation issues, summary counts, traversal limits, and the pure validator. It has no IronCAD or WPF dependencies.

Validation marks missing properties, duplicate NodeId/ItemCode, inconsistent ProjectCode/Revision, ItemType-kind mismatch, invalid parent/path relationships, and SceneName mismatch. Validation never suppresses nodes from the viewer.

### IronCAD read adapter

`IdeaCadConnector.IronCAD/PdmModel/IronCadPdmModelReader` traverses `IZElement` deterministically. It reads custom properties and element metadata only. A cycle guard, maximum depth, and maximum node count stop unsafe traversal.

`IronCadReadPdmCommand` captures the active document path, modified state, and SHA-256 before reading; reads and validates the snapshot; verifies the three invariants again; then opens the viewer. A changed invariant raises `SOURCE_FILE_CHANGED_DURING_READ` and does not show stale data.

### Viewer

`IdeaCadConnector.Ui` owns `PdmModelViewerViewModel` and `PdmModelViewer`. The view model projects the workspace snapshot into rows and summary values. The WPF window is read-only and shows Level, Type, Item Code, Display Name, Scene Name, Revision, Node ID, Parent, Occurrence Path, and Status.

## Data flow

1. User opens a saved `.ics` Scene and clicks `Đọc dữ liệu PDM`.
2. Command captures path, modified state, and file hash.
3. Reader traverses the active Scene and creates a snapshot.
4. Pure validator attaches issues and computes summary counts.
5. Command verifies path, modified state, and hash are unchanged.
6. Viewer displays every node and the aggregate summary.

## Failure behavior

- Missing or non-scene active document: show a stable read error.
- Traversal cycle/limit: fail with a stable traversal code; never recurse indefinitely.
- Missing/duplicate business metadata: show the viewer and mark affected nodes.
- Changed path, modified state, or hash: fail with `SOURCE_FILE_CHANGED_DURING_READ`.
- COM `E_FAIL` for unavailable optional properties is treated as unavailable data; other COM failures remain visible errors.

## Verification

- Pure unit tests cover validation, stable paths, cycle/depth/node limits, and view-model projection.
- Reader contract tests verify the production reader contains no forbidden write API calls.
- Debug and Release solution builds and tests must pass.
- Runtime uses only a StudyCase copy and records counts, validation totals, hash equality, and modified-state equality.
