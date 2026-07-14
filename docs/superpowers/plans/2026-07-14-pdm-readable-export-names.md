# PDM Readable Export Names Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export one normalized package named by project code and remove the `ASM`/`PRT` token from canonical CAD filenames.

**Architecture:** Keep deterministic naming in `PdmNameNormalizer`, isolate final/pending directory construction in a workspace path policy, and extend the existing publication transaction to replace an existing final package only after the new pending package has passed validation. The IronCAD command continues to write into a same-volume pending directory so closed files can be renamed atomically into the final project-code directory.

**Tech Stack:** C# 8, .NET Framework 4.8, xUnit, IronCAD COM integration, Newtonsoft.Json.

## Global Constraints

- Final package directory is `<OutputFolder>/<ProjectCode>`.
- CAD filename is `<ProjectCode>__<ItemCode>__<DisplayName>.ics`.
- Do not include `ASM` or `PRT` in CAD filenames.
- Do not delete an existing final package until the new pending package passes validation.
- Do not retain a backup after successful replacement.
- Manifest schema remains version 2 and continues to carry `itemType`.
- Duplicate canonical filenames block export before replacement.

---

### Task 1: Simplify canonical CAD filenames

**Files:**
- Modify: `tests/IdeaCadConnector.Tests/PdmNormalizationTests.cs`
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmNameNormalizer.cs`

**Interfaces:**
- Consumes: `PdmNameNormalizer.CreateCanonicalFileName(string projectCode, string type, string itemCode, string displayName)`.
- Produces: the unchanged method signature with output `<ProjectCode>__<ItemCode>__<DisplayName>.ics`; `type` remains validated for manifest/planner compatibility.

- [ ] **Step 1: Write failing expectations**

Change the planner and final-plan assertions to:

```csharp
Assert.Equal("PDM-STUDYCASE__ASM-001__ASSEMBLY-001.ics", plan.Assemblies.Single().CanonicalFileName);
Assert.Equal("PDM-NEW__B02__NEW-NAME.ics", finalPlan.Parts.Single().CanonicalFileName);
```

Add a direct assertion proving both item types use the same human-readable layout:

```csharp
[Theory]
[InlineData("ASM")]
[InlineData("PRT")]
public void CanonicalFileName_OmitsItemTypeToken(string itemType)
{
    Assert.Equal("PDM-DEMO__A01__MAIN-BODY.ics",
        PdmNameNormalizer.CreateCanonicalFileName("PDM-DEMO", itemType, "A01", "MAIN-BODY"));
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~PdmNormalizationTests
```

Expected: failures show actual filenames still contain `__ASM__` or `__PRT__`.

- [ ] **Step 3: Implement the minimal formatter change**

Keep the existing item-type validation, then return:

```csharp
return name + "__" + code + "__" + display + ".ics";
```

- [ ] **Step 4: Re-run focused tests and verify GREEN**

Run the command from Step 2. Expected: all `PdmNormalizationTests` pass.

---

### Task 2: Build readable final and private pending package paths

**Files:**
- Create: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackagePublicationPaths.cs`
- Modify: `tests/IdeaCadConnector.Tests/PdmNormalizeExportSafetyTests.cs`

**Interfaces:**
- Produces: `PdmPackagePublicationPaths.Create(string outputFolder, string projectCode, string nonce)` returning `FinalDirectory` and `PendingDirectory`.
- Final directory is `<output>/<normalized project code>`.
- Pending directory is `<output>/.<normalized project code>.pending-<nonce>`.

- [ ] **Step 1: Write the failing path-policy test**

```csharp
[Fact]
public void PublicationPaths_UseProjectCodeForFinalAndPrivateUniquePending()
{
    var output = Path.Combine(Path.GetTempPath(), "pdm-output");
    var paths = PdmPackagePublicationPaths.Create(output, "pdm studycase", "abc123");

    Assert.Equal(Path.Combine(output, "PDM-STUDYCASE"), paths.FinalDirectory);
    Assert.Equal(Path.Combine(output, ".PDM-STUDYCASE.pending-abc123"), paths.PendingDirectory);
}
```

- [ ] **Step 2: Run the safety test and verify RED**

Run:

```powershell
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~PdmNormalizeExportSafetyTests.PublicationPaths
```

Expected: compilation fails because `PdmPackagePublicationPaths` does not exist.

- [ ] **Step 3: Implement the path policy**

Create an immutable result with `FinalDirectory` and `PendingDirectory`. In `Create`, normalize the project code using `PdmNameNormalizer.NormalizeProjectCode`, reject an empty nonce with `ArgumentException`, and use `Path.GetFullPath(Path.Combine(...))` for both paths.

- [ ] **Step 4: Re-run the focused safety test and verify GREEN**

Run the command from Step 2. Expected: pass.

---

### Task 3: Replace an existing package only after pending validation

**Files:**
- Modify: `src/IdeaCadConnector.Workspace/NormalizeExport/PdmPackagePublicationTransaction.cs`
- Modify: `tests/IdeaCadConnector.Tests/PdmNormalizeExportSafetyTests.cs`

**Interfaces:**
- Produces: `CommitPendingReplacingFinal()` on `PdmPackagePublicationTransaction`.
- `MoveToPending()` accepts `StagingDirectory == PendingDirectory` as an already-staged no-op when that directory exists.

- [ ] **Step 1: Write failing replacement tests**

```csharp
[Fact]
public void CommitPendingReplacingFinal_DeletesOldPackageAndPublishesNewPackage()
{
    var root = Path.Combine(Path.GetTempPath(), "pdm-replace-" + Guid.NewGuid().ToString("N"));
    var pending = Path.Combine(root, ".pending");
    var final = Path.Combine(root, "PDM-DEMO");
    Directory.CreateDirectory(pending);
    Directory.CreateDirectory(final);
    File.WriteAllText(Path.Combine(pending, "new.marker"), "new");
    File.WriteAllText(Path.Combine(final, "old.marker"), "old");

    var transaction = new PdmPackagePublicationTransaction(pending, pending, final);
    transaction.MoveToPending();
    transaction.CommitPendingReplacingFinal();

    Assert.False(Directory.Exists(pending));
    Assert.False(File.Exists(Path.Combine(final, "old.marker")));
    Assert.True(File.Exists(Path.Combine(final, "new.marker")));
    Directory.Delete(root, true);
}
```

Add a second test where pending is absent and assert `PACKAGE_COMMIT_FAILED` while the old final marker remains.

- [ ] **Step 2: Run the two replacement tests and verify RED**

Run:

```powershell
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~CommitPendingReplacingFinal
```

Expected: compilation fails because the replacement method does not exist.

- [ ] **Step 3: Implement replacement semantics**

In `MoveToPending`, return without moving when staging and pending are equal and the directory exists. Implement `CommitPendingReplacingFinal()` to validate pending exists, delete final recursively if present, then call the existing retrying directory move. Wrap IO and access failures in `PdmNormalizeExportException` with code `PACKAGE_COMMIT_FAILED`.

- [ ] **Step 4: Re-run replacement and existing transaction tests**

Run:

```powershell
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~PdmNormalizeExportSafetyTests
```

Expected: all safety tests pass, including the existing non-replacement commit tests.

---

### Task 4: Wire IronCAD export to project-code replacement publication

**Files:**
- Modify: `src/IdeaCadConnector.IronCAD/NormalizeExport/IronCadNormalizeExportCommand.cs`
- Modify: `tests/IdeaCadConnector.Tests/PdmNormalizeExportSafetyTests.cs`

**Interfaces:**
- Consumes: `PdmPackagePublicationPaths.Create(...)` and `PdmPackagePublicationTransaction.CommitPendingReplacingFinal()`.
- Produces: a verified package at `<OutputFolder>/<ProjectCode>` with no timestamp/GUID in the final directory name.

- [ ] **Step 1: Add a source-contract test for command wiring**

Read `IronCadNormalizeExportCommand.cs` in the test and assert it contains calls to `PdmPackagePublicationPaths.Create` and `CommitPendingReplacingFinal`, and does not contain the old `var packageName = "PDM-"` construction. This test protects the COM orchestration path that cannot be instantiated safely without IronCAD.

- [ ] **Step 2: Run the source-contract test and verify RED**

Run:

```powershell
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --filter FullyQualifiedName~NormalizeExportCommand_UsesReadableReplacementPaths
```

Expected: failure because the command still constructs a timestamp/GUID final directory.

- [ ] **Step 3: Wire the command**

Create paths after final-plan preflight:

```csharp
var publicationPaths = PdmPackagePublicationPaths.Create(
    dialog.Result.OutputFolder,
    finalPlan.ProjectCode,
    Guid.NewGuid().ToString("N"));
finalDirectory = publicationPaths.FinalDirectory;
pendingDirectory = publicationPaths.PendingDirectory;
var packageStaging = pendingDirectory;
```

Validate `pendingDirectory` with `PdmOutputSafetyValidator` so an existing final directory is allowed. Keep all current staged/pending/final package verification. After closing `pendingPackageDoc`, call `publication.CommitPendingReplacingFinal()` before opening and verifying the final package.

- [ ] **Step 4: Re-run command and normalization tests**

Run:

```powershell
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~PdmNormalizationTests|FullyQualifiedName~PdmNormalizeExportSafetyTests"
```

Expected: all selected tests pass.

---

### Task 5: Full verification and publication preparation

**Files:**
- Verify all modified source and test files.

**Interfaces:**
- Produces: Debug and Release builds with the complete test suite passing.

- [ ] **Step 1: Run Debug build and tests**

```powershell
dotnet build .\IdeaCadConnector.sln --configuration Debug --no-restore
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-build --no-restore
```

Expected: zero warnings/errors and all tests pass.

- [ ] **Step 2: Run Release build and tests**

```powershell
dotnet build .\IdeaCadConnector.sln --configuration Release --no-restore
dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Release --no-build --no-restore
```

Expected: zero warnings/errors and all tests pass.

- [ ] **Step 3: Review the final diff for private paths and unrelated files**

Run `git diff --check`, inspect `git status -sb`, and confirm no StudyCase absolute path or generated package is staged.

- [ ] **Step 4: Commit the implementation**

Stage only the naming, publication, command, and test files, then commit:

```powershell
git commit -m "feat(pdm): simplify normalized export names"
```
