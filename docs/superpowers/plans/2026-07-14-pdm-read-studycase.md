# IDEA PDM StudyCase BOM Reader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read the active normalized IronCAD StudyCase without writes and display its complete validated BOM in a read-only IDEA PDM viewer.

**Architecture:** Pure BOM models and validation live in Workspace, the COM-only traversal adapter lives in IronCAD, and WPF projection/view live in Ui. The command captures and verifies path, modified state, and SHA-256 around the read before showing the viewer.

**Tech Stack:** C#/.NET Framework 4.8, IronCAD ICAPI COM interop, WPF, xUnit.

## Global Constraints

- Never call Save, SaveAs, `element.Name =`, AddCustomPropString, SetProperty, or CloseFile.
- Never modify the source `.ics` or document Modified state.
- Do not call Aras or change Normalize Export.
- Traversal must have cycle, depth, and node-count guards with deterministic occurrence paths.
- Validation marks nodes but never removes invalid nodes from display.

---

### Task 1: Pure BOM model and validator

**Files:**
- Create: `src/IdeaCadConnector.Workspace/PdmModel/PdmModelModels.cs`
- Create: `src/IdeaCadConnector.Workspace/PdmModel/PdmModelValidator.cs`
- Create: `tests/IdeaCadConnector.Tests/PdmModelValidatorTests.cs`

**Interfaces:**
- Produces: `PdmModelNode`, `PdmModelSnapshot`, `PdmModelValidationResult`, `PdmModelValidator.Validate(PdmModelSnapshot)`.

- [ ] Write failing tests for complete metadata, missing fields, duplicate IDs/codes, inconsistent project/revision, wrong item type, invalid parent/path, and SceneName mismatch.
- [ ] Run `dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~PdmModelValidatorTests` and confirm missing-type failures.
- [ ] Implement models with the exact properties from the specification and validator issue strings used by the viewer.
- [ ] Re-run the focused tests and confirm pass.

### Task 2: Deterministic guarded traversal core

**Files:**
- Create: `src/IdeaCadConnector.Workspace/PdmModel/PdmModelTraversal.cs`
- Create: `tests/IdeaCadConnector.Tests/PdmModelTraversalTests.cs`

**Interfaces:**
- Consumes: `PdmModelNode`.
- Produces: `PdmModelTraversalLimits`, `PdmModelTraversalGuard<T>`, deterministic path helper using `0`, `0/0`, `0/1`.

- [ ] Write failing tests for stable occurrence paths, cycle detection, max depth, and max node count.
- [ ] Run focused tests and confirm expected missing-type failures.
- [ ] Implement the minimal guard and path helper.
- [ ] Re-run focused tests and confirm pass.

### Task 3: IronCAD read-only reader and integrity command

**Files:**
- Create: `src/IdeaCadConnector.IronCAD/PdmModel/IronCadPdmModelReader.cs`
- Create: `src/IdeaCadConnector.IronCAD/PdmModel/IronCadReadPdmCommand.cs`
- Modify: `src/IdeaCadConnector.IronCAD/IronCadAddin.cs`
- Create: `tests/IdeaCadConnector.Tests/IronCadPdmReadOnlyContractTests.cs`

**Interfaces:**
- Consumes: active `IZSceneDoc`, `PdmModelTraversalLimits`, `PdmSourceIntegrity`.
- Produces: `IronCadPdmModelReader.Read(IZSceneDoc)` and `IronCadReadPdmCommand.Execute()`.

- [ ] Write a source-contract test that rejects forbidden write API tokens in the two new production files.
- [ ] Run the contract test and confirm it fails because files/types do not exist.
- [ ] Implement traversal that only calls getters, custom-property reads, and child enumeration.
- [ ] Implement before/after path, Modified, and SHA-256 checks with `SOURCE_FILE_CHANGED_DURING_READ`.
- [ ] Add a ribbon command `Đọc dữ liệu PDM` beside the existing Normalize Export command.
- [ ] Re-run contract and model tests.

### Task 4: Read-only PDM Model Viewer

**Files:**
- Create: `src/IdeaCadConnector.Ui/ViewModels/PdmModelViewerViewModel.cs`
- Create: `src/IdeaCadConnector.Ui/Views/PdmModelViewer.xaml`
- Create: `src/IdeaCadConnector.Ui/Views/PdmModelViewer.xaml.cs`
- Create: `tests/IdeaCadConnector.Tests/PdmModelViewerViewModelTests.cs`

**Interfaces:**
- Consumes: validated `PdmModelSnapshot`.
- Produces: summary properties and read-only row collection for WPF binding.

- [ ] Write failing projection tests asserting valid and invalid nodes remain visible and summary counts are correct.
- [ ] Run focused tests and confirm missing-view-model failure.
- [ ] Implement the view model projection.
- [ ] Implement a read-only DataGrid with required columns and summary header.
- [ ] Re-run focused tests and confirm pass.

### Task 5: Verification and runtime evidence

**Files:**
- Create outside Git: runtime evidence under the existing PDM runtime test directory.

**Interfaces:**
- Consumes: built add-in and a disposable StudyCase copy.
- Produces: counts, validation totals, hash equality, modified-state equality, and viewer screenshot/runtime description.

- [ ] Run Debug solution build and tests.
- [ ] Run Release solution build and tests.
- [ ] Open only the runtime copy in IronCAD and click `Đọc dữ liệu PDM`.
- [ ] Record root/project/revision/count/depth/validity values without committing private paths or CAD names.
- [ ] Verify SHA-256 and Modified state are unchanged.
- [ ] Commit with `feat(pdm): read normalized IronCAD model into IDEA PDM`.
- [ ] Push branch and create draft PR titled `feat: read normalized StudyCase into IDEA PDM`; do not merge.
