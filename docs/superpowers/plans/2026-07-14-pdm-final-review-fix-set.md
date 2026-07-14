# PDM Final Review Fix Set Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close all six final-review findings with regression-tested publication safety, fail-closed IronCAD traversal, and lossless manifest BOM import.

**Architecture:** Validate publication paths at project-code normalization, path construction, transaction construction, and immediately before recursive deletion. Keep manifest validation non-throwing, project BOM-v2 quantities into the existing business model, and let the existing Desktop/push pipelines consume that model unchanged except for quantity propagation.

**Tech Stack:** C#/.NET Framework 4.8, xUnit, Newtonsoft.Json, WPF/MVVM, Windows reparse points.

## Global Constraints

- Work directly in the current checkout and preserve unrelated work.
- Add a failing regression test before every production-code behavior change.
- Do not push or modify the registry.
- Do not require an IronCAD runtime for automated tests.
- Skip a reparse-point test only when the platform cannot create the requested link.
- Run focused tests and then the complete Debug test suite.

---

### Task 1: Guard publication paths and cleanup

**Files:**
- Modify: `tests/IdeaCadConnector.Tests/PdmNormalizeExportSafetyTests.cs`
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmNameNormalizer.cs`
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackagePublicationPaths.cs`
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackagePublicationTransaction.cs`
- Modify: `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadNormalizeExportCommand.cs`

**Interfaces:**
- Consumes: output folder, normalized project code, pending nonce, transaction paths.
- Produces: final/pending direct-child guarantees and guarded `RollbackPending`/`RollbackFinal` cleanup.

- [ ] **Step 1: Write failing path and cleanup tests**

Add theories rejecting `.`, `..`, and normalized codes beginning with punctuation; transaction construction tests rejecting non-sibling publication paths; a command source-contract test requiring transaction creation before package writes and guarded rollback; and a Windows junction test that verifies the external target marker survives rejection.

```csharp
Assert.Throws<ArgumentException>(() => PdmNameNormalizer.NormalizeProjectCode(".."));
Assert.Throws<PdmNormalizeExportException>(() =>
    new PdmPackagePublicationTransaction(pending, pending, outsideFinal));
Assert.True(File.Exists(Path.Combine(target, "keep.marker")));
```

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter "FullyQualifiedName~PdmNormalizeExportSafetyTests"`

Expected: new project-code, direct-child, source-contract, and reparse tests fail for missing safeguards.

- [ ] **Step 3: Implement layered path safety**

Require normalized project codes to match `^[A-Z0-9]`, canonicalize the output root, prove final/pending are distinct direct children, reject existing reparse components, and repeat the checks immediately before `Directory.Delete(path, true)`.

```csharp
if (result == "." || result == ".." || !Regex.IsMatch(result, @"^[A-Z0-9]"))
    throw new ArgumentException("Project code is invalid.", nameof(value));
```

Construct `PdmPackagePublicationTransaction` before creating staging/package directories. Replace direct publication-directory cleanup calls with guarded transaction rollback while retaining staging cleanup.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Task 1 focused command and require exit code 0.

- [ ] **Step 5: Commit publication safety**

Commit message: `fix(pdm): harden package publication safety`

### Task 2: Fail closed on IronCAD child traversal

**Files:**
- Modify: `tests/IdeaCadConnector.Tests/PdmNormalizeExportSafetyTests.cs`
- Modify: `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadDependencyDiscovery.cs`

**Interfaces:**
- Consumes: `IZElement.GetChildrenZArray()` and `IZSceneElement.ModelLinkPath`.
- Produces: propagated `DEPENDENCY_DISCOVERY_FAILED` for child enumeration while suppressing only known unavailable `ModelLinkPath` failures.

- [ ] **Step 1: Write the failing source/behavior regression test**

Assert the source contains a guarded `ModelLinkPath` read and a fail-closed `GetChildrenZArray` catch, and does not reuse `IsIgnorableModelLinkPathFailure` for child enumeration.

```csharp
Assert.DoesNotContain("catch (Exception ex) when (IsIgnorableModelLinkPathFailure(ex)) { children = null; }", source);
Assert.Contains("throw new InvalidOperationException(\"DEPENDENCY_DISCOVERY_FAILED\", ex);", source);
```

- [ ] **Step 2: Run the single test and verify RED**

Run: `dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter "FullyQualifiedName~GetChildrenZArray"`

Expected: source contract fails because E_FAIL is currently converted to an empty child list.

- [ ] **Step 3: Implement fail-closed traversal**

Wrap `GetChildrenZArray` exceptions as `InvalidOperationException("DEPENDENCY_DISCOVERY_FAILED", ex)` and retain the filtered suppression only around `ModelLinkPath`.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run both the single test and all `PdmNormalizeExportSafetyTests`; require exit code 0.

### Task 3: Preserve BOM quantities and tolerate duplicate occurrence IDs

**Files:**
- Modify: `tests/IdeaCadConnector.Tests/PdmPackageImportReaderTests.cs`
- Modify: `tests/IdeaCadConnector.Tests/PdmProjectsManifestIntegrationTests.cs`
- Modify: `src/IdeaCadConnector.Workspace/PdmNamingPolicy.cs`
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackageValidator.cs`
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackageImportReader.cs`
- Modify: `src/IdeaCadConnector.Workspace/PushPreviewMapper.cs`
- Modify: `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs`

**Interfaces:**
- Consumes: BOM-v2 `(ParentOccurrenceId, ChildDefinitionId, Quantity)` and occurrence arrays that may contain duplicate IDs.
- Produces: `PdmBusinessNode.Quantity`, Desktop `PdmStructureNode.Quantity`, and `AnalyzedStructureNode.Quantity`; malformed duplicates produce blocking issues without throwing.

- [ ] **Step 1: Write failing reader and end-to-end quantity tests**

Set the part BOM quantity to `3`; assert `BusinessStructure.RootNodes[0].Quantity`, Desktop tree quantity, mapped analyzed structure quantity, and push-preview part quantity all equal `3`.

```csharp
Assert.Equal(3, child.Quantity);
Assert.Equal(3, preview.Parts.Single(part => part.LogicalCode == "P01").Quantity);
```

- [ ] **Step 2: Write failing malformed duplicate test**

Append an occurrence with an existing `OccurrenceId`, read the package, and assert `DuplicateManifestId` plus a blocking folder issue instead of an exception.

```csharp
var result = new PdmPackageImportReader().Read(folder);
Assert.Contains(PdmPackageValidationIssue.DuplicateManifestId, result.Validation.Issues);
Assert.Contains(result.FolderAnalysis.Issues, issue => issue.BlocksPush);
```

- [ ] **Step 3: Run importer/integration tests and verify RED**

Run: `dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter "FullyQualifiedName~PdmPackageImportReaderTests|FullyQualifiedName~PdmProjectsManifestIntegrationTests"`

Expected: quantity assertions report `1`, and duplicate occurrence input throws from cycle validation.

- [ ] **Step 4: Implement minimal manifest projection fixes**

Give `PdmBusinessNode.Quantity` a legacy-safe default of one. Build deterministic first-occurrence dictionaries for validation/mapping. Resolve each business node quantity from the matching BOM-v2 edge, and pass it through Desktop and `PushPreviewMapper`.

```csharp
public int Quantity { get; set; } = 1;
Quantity = ResolveQuantity(occurrence, bom),
```

- [ ] **Step 5: Run importer/integration tests and verify GREEN**

Run the Task 3 focused command and require exit code 0.

- [ ] **Step 6: Commit manifest integrity**

Commit message: `fix(pdm): preserve imported BOM integrity`

### Task 4: Final verification and review

**Files:**
- Review every changed source, test, spec, and plan file.

**Interfaces:**
- Consumes: committed Task 1-3 behavior.
- Produces: a clean intentional commit set with reproducible test evidence.

- [ ] **Step 1: Run all focused tests**

Run the Task 1 and Task 3 focused commands and require zero failures.

- [ ] **Step 2: Run complete Debug tests**

Run: `dotnet test IdeaCadConnector.sln -c Debug`

Expected: exit code 0 and zero failed tests.

- [ ] **Step 3: Inspect repository state**

Run: `git diff --check`, `git status --short`, and `git log --oneline` to verify no unrelated or unstaged changes remain.

- [ ] **Step 4: Independent code review**

Review the final commit range against all six findings; resolve every Critical or Important issue before reporting completion.
