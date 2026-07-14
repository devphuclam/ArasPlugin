# IDEA PDM Manifest BOM Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make IDEA PDM Desktop read normalized package manifests and display their BOM in its existing project tree while retaining legacy naming fallback.

**Architecture:** A pure Workspace reader validates and maps manifest v2 into the existing folder/business analysis models. `PdmProjectsViewModel` chooses manifest-v2 analysis when available and otherwise preserves the old analyzer path.

**Tech Stack:** C#/.NET Framework 4.8, Newtonsoft.Json, WPF/MVVM, xUnit.

## Global Constraints

- Do not modify Normalize Export or IronCAD add-in behavior.
- Do not parse, write, rename, save, or open `.ics` files.
- Do not call or push to Aras.
- Display readable BOM nodes even when manifest validation reports issues.
- Preserve legacy naming behavior when no manifest exists.

---

### Task 1: Manifest discovery and mapping

**Files:**
- Create: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackageImportReader.cs`
- Create: `tests/IdeaCadConnector.Tests/PdmPackageImportReaderTests.cs`

**Interfaces:**
- Produces: `PdmPackageImportReader.FindManifest(string)`, `TryRead(string, out PdmPackageImportResult)`, and analyses consumed by Desktop.

- [ ] Write failing tests for discovery, project/revision mapping, definitions, occurrence hierarchy, and latest manifest selection.
- [ ] Run focused tests and confirm missing-type failures.
- [ ] Implement safe deserialization, existing validator invocation, and deterministic mapping.
- [ ] Re-run focused tests and confirm pass.

### Task 2: Validation and malformed-package behavior

**Files:**
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackageImportReader.cs`
- Modify: `tests/IdeaCadConnector.Tests/PdmPackageImportReaderTests.cs`

**Interfaces:**
- Produces: validation issues represented as blocking `PdmNamingIssue` while retaining readable nodes.

- [ ] Write failing tests for missing files, duplicate codes/IDs, invalid hierarchy, malformed JSON, and invalid schema.
- [ ] Implement non-destructive issue propagation and stable error messages.
- [ ] Re-run focused tests and confirm pass.

### Task 3: IDEA PDM Desktop integration

**Files:**
- Modify: `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs`
- Create: `tests/IdeaCadConnector.Tests/PdmProjectsManifestIntegrationTests.cs`

**Interfaces:**
- Consumes: `PdmPackageImportResult`.
- Produces: existing `PdmStructure`, `CadStructure`, documents, summary, and `NamingPolicyVersion` populated from manifest v2.

- [ ] Write failing integration tests showing manifest-v2 BOM roots/children and legacy fallback.
- [ ] Update `AnalyzeFolder()` to prefer manifest analysis without changing legacy analyzer code.
- [ ] Re-run focused tests and confirm pass.

### Task 4: Full verification and runtime

**Files:**
- No committed private runtime artifacts.

- [ ] Build/test Debug and Release.
- [ ] Launch IDEA PDM Desktop and select the exported StudyCase package.
- [ ] Confirm existing PDM structure shows root, assemblies, and parts from the manifest.
- [ ] Commit `feat(pdm): read normalized IronCAD model into IDEA PDM`.
- [ ] Push branch and create draft PR; do not merge.
