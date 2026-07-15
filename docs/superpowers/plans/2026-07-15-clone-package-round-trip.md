# Clone Package Round-Trip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing Aras Clone operation emit the same `.idea-pdm` + `cad` + manifest V2 package layout produced by Normalize & Export while preserving the existing live Part/BOM/CAD retrieval behavior.

**Architecture:** Keep Aras traversal and vault download in `HttpPdmRepositoryClient`, but move deterministic package-manifest construction into a focused Workspace service. Add a small vault-download interface so the repository Clone path can be tested without a live Aras vault. Clone downloads into an external temporary package, validates it with the existing V2 validator, then publishes it into the selected project folder.

**Tech Stack:** C# 12 syntax targeting .NET Framework 4.8, xUnit 2.4.2, Newtonsoft.Json 13.0.4, Aras AML/OData, existing `PdmPackageValidator`, `PdmPackageManifestWriter`, and `WorkspaceService`.

## Global Constraints

- Keep the existing live Aras Part/BOM/CAD/native-file retrieval model; do not add archives, snapshots, or a new server schema.
- Required top-level output is exactly `.idea-pdm/`, `cad/`, and `pdm-bom-manifest.json`.
- Preserve native filenames stored by Push and place every `.ics` under `cad/`.
- Do not generate `ARAS01`, empty PDF/DWG placeholders, or `*-STRUCTURE.txt`.
- `RootFile` and every definition filename use package-relative forward-slash paths such as `cad/PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics`.
- A Clone failure must not publish a partial package or report placeholder files as success.
- Keep the current warning that non-main branches resolve latest live Aras data.

---

## File map

- Create `src/IdeaCadConnector.Aras/IVaultFileClient.cs`: injectable vault download boundary.
- Modify `src/IdeaCadConnector.Aras/VaultClient.cs`: implement the boundary without changing HTTP behavior.
- Create `src/IdeaCadConnector.Workspace/Clone/PdmClonePackageBuilder.cs`: package input models, canonical-name parsing, manifest construction, branch registry creation, and validation.
- Modify `src/IdeaCadConnector.Aras/IdeaCadConnector.Aras.csproj`: reference Workspace so Aras can call the package builder.
- Modify `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs`: preserve BOM edges, download into `cad`, invoke builder, publish atomically, and remove legacy placeholder generation.
- Modify `src/IdeaCadConnector.Core/Contracts/IPdmRepositoryClient.cs`: expose `RootCadFilePath`; retain `PlaceholderDocumentCount` as a compatibility property fixed at zero.
- Modify `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs`: stop clearing freshly cloned workspace metadata and report the new package result.
- Create `tests/IdeaCadConnector.Tests/PdmClonePackageBuilderTests.cs`: deterministic package and validator tests.
- Create `tests/IdeaCadConnector.Tests/PdmCloneRoundTripTests.cs`: repository-level Clone tests with fake AML and vault clients.
- Modify `tests/IdeaCadConnector.Tests/PdmProjectsManifestIntegrationTests.cs`: desktop analysis and Clone-success behavior.

---

### Task 1: Add a testable vault download boundary

**Files:**
- Create: `src/IdeaCadConnector.Aras/IVaultFileClient.cs`
- Modify: `src/IdeaCadConnector.Aras/VaultClient.cs`
- Modify: `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:14-42`
- Test: `tests/IdeaCadConnector.Tests/PdmCloneRoundTripTests.cs`

**Interfaces:**
- Produces: `Task<string> IVaultFileClient.DownloadFileAsync(string fileId, string targetDirectory, CancellationToken ct)`.
- Consumed by: Task 3 repository Clone implementation.

- [ ] **Step 1: Write the failing constructor/injection test**

```csharp
[Fact]
public void CloneClient_AcceptsInjectedVaultDownloader()
{
    var aml = new CloneAmlClient();
    var vault = new CloneVaultClient();
    var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "db" };

    using var client = new HttpPdmRepositoryClient(
        options, aml, vault, NullLogger<HttpPdmRepositoryClient>.Instance);

    Assert.NotNull(client);
}
```

- [ ] **Step 2: Run the focused test and verify it fails to compile**

Run:

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter CloneClient_AcceptsInjectedVaultDownloader
```

Expected: FAIL because `IVaultFileClient` and the four-argument internal constructor do not exist.

- [ ] **Step 3: Add the interface and implement it in `VaultClient`**

```csharp
namespace IdeaCadConnector.Aras
{
    internal interface IVaultFileClient
    {
        Task<string> DownloadFileAsync(
            string fileId,
            string targetDirectory,
            CancellationToken ct);
    }
}
```

Change the class declaration to:

```csharp
internal sealed class VaultClient : IVaultFileClient
```

Change the repository field and add the test constructor:

```csharp
private IVaultFileClient _vault;

internal HttpPdmRepositoryClient(
    ArasClientOptions options,
    IArasAmlClient amlClient,
    IVaultFileClient vaultClient,
    ILogger<HttpPdmRepositoryClient> logger = null)
{
    _options = options ?? throw new ArgumentNullException(nameof(options));
    _aml = amlClient ?? throw new ArgumentNullException(nameof(amlClient));
    _vault = vaultClient ?? throw new ArgumentNullException(nameof(vaultClient));
    _logger = logger ?? NullLogger<HttpPdmRepositoryClient>.Instance;
}
```

Keep the existing three-argument internal constructor for current tests and callers.

- [ ] **Step 4: Run the focused test**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 5: Commit the boundary**

```powershell
git add src/IdeaCadConnector.Aras/IVaultFileClient.cs src/IdeaCadConnector.Aras/VaultClient.cs src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs tests/IdeaCadConnector.Tests/PdmCloneRoundTripTests.cs
git commit -m "test(pdm): inject vault downloads for clone"
```

---

### Task 2: Build and validate the normalized Clone package

**Files:**
- Create: `src/IdeaCadConnector.Workspace/Clone/PdmClonePackageBuilder.cs`
- Test: `tests/IdeaCadConnector.Tests/PdmClonePackageBuilderTests.cs`

**Interfaces:**
- Consumes: a package root already containing downloaded native files under `cad/`.
- Produces: `PdmClonePackageBuildResult PdmClonePackageBuilder.Build(PdmClonePackageInput input)`.
- Produces models: `PdmClonePackageInput`, `PdmCloneNode`, `PdmCloneBomEdge`, and `PdmClonePackageBuildResult`.

- [ ] **Step 1: Write a failing exact-layout round-trip test**

```csharp
[Fact]
public void Build_WritesPushCompatibleManifestAndBranchRegistry()
{
    using var folder = new TempFolder();
    var cad = Path.Combine(folder.Path, "cad");
    Directory.CreateDirectory(cad);
    File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), new byte[] { 1 });
    File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__A01__BASE.ics"), new byte[] { 2 });

    var result = new PdmClonePackageBuilder().Build(new PdmClonePackageInput
    {
        PackageRoot = folder.Path,
        ProjectCode = "PDM-STUDYCASE",
        Revision = "A",
        BranchName = "main",
        RootNodeId = "root-part",
        Nodes = new[]
        {
            new PdmCloneNode { NodeId = "root-part", ItemCode = "ROOT", ItemType = "ASM", DisplayName = "PDM-STUDYCASE", Revision = "A", NativeFileName = "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics" },
            new PdmCloneNode { NodeId = "part-a01", ItemCode = "A01", ItemType = "PRT", DisplayName = "BASE", Revision = "A", NativeFileName = "PDM-STUDYCASE__A01__BASE.ics" }
        },
        Edges = new[] { new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 2, SortOrder = 10 } }
    });

    Assert.True(result.Success, result.ErrorMessage);
    Assert.Equal("cad/PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics", result.Manifest.RootFile);
    Assert.Equal(2, result.Manifest.Definitions.Count());
    Assert.Equal(2, result.Manifest.Occurrences.Count());
    Assert.Equal(2m, result.Manifest.BomV2.Single().Quantity);
    Assert.True(File.Exists(Path.Combine(folder.Path, "pdm-bom-manifest.json")));
    Assert.True(File.Exists(Path.Combine(folder.Path, ".idea-pdm", "branches.json")));
    Assert.True(new PdmPackageImportReader().Read(folder.Path).Validation.IsValid);
}
```

Define a private `TempFolder : IDisposable` in `PdmClonePackageBuilderTests.cs`; its constructor creates `Path.Combine(Path.GetTempPath(), "pdm-clone-builder-" + Guid.NewGuid().ToString("N"))` and `Dispose` recursively deletes that directory.

- [ ] **Step 2: Run the builder test and verify it fails**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter PdmClonePackageBuilderTests
```

Expected: FAIL because the Clone package builder and models do not exist.

- [ ] **Step 3: Implement deterministic manifest construction**

Implement `Build` with these concrete rules:

```csharp
public PdmClonePackageBuildResult Build(PdmClonePackageInput input)
{
    ValidateInput(input);
    var nodes = input.Nodes.ToDictionary(n => n.NodeId, StringComparer.OrdinalIgnoreCase);
    var orderedEdges = input.Edges
        .OrderBy(e => e.ParentNodeId, StringComparer.OrdinalIgnoreCase)
        .ThenBy(e => e.SortOrder)
        .ThenBy(e => e.ChildNodeId, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var occurrences = BuildOccurrences(input.RootNodeId, orderedEdges);
    var manifest = new PdmPackageManifest
    {
        SchemaVersion = 2,
        ProjectCode = PdmNameNormalizer.NormalizeProjectCode(input.ProjectCode),
        Revision = input.Revision,
        RootNodeId = input.RootNodeId,
        RootItemCode = nodes[input.RootNodeId].ItemCode,
        RootOccurrenceId = occurrences.Single(o => o.ParentOccurrenceId == null).OccurrenceId,
        RootFile = ToCadRelativePath(nodes[input.RootNodeId].NativeFileName),
        Definitions = nodes.Values.OrderBy(n => n.NodeId, StringComparer.OrdinalIgnoreCase).Select(ToDefinition).ToArray(),
        Occurrences = occurrences,
        BomV2 = ToBomEdges(occurrences, orderedEdges, nodes),
        Warnings = Array.Empty<string>()
    };

    File.WriteAllText(
        Path.Combine(input.PackageRoot, PdmPackageImportReader.ManifestFileName),
        new PdmPackageManifestWriter().Serialize(manifest));
    WriteBranches(input.PackageRoot, input.BranchName);
    var validation = new PdmPackageValidator().Validate(input.PackageRoot, manifest);
    return validation.IsValid
        ? PdmClonePackageBuildResult.Ok(manifest)
        : PdmClonePackageBuildResult.Fail("Clone package validation failed: " + string.Join(", ", validation.Issues));
}
```

Use forward slashes in manifest paths. Reject rooted names, directory separators, `.`/`..`, files outside `cad`, duplicate filenames, missing root, cycles, and missing CAD files. Write `branches.json` with `WorkspaceService.SaveBranchRegistry`; include `main` and include the selected branch once when it is not `main`.

- [ ] **Step 4: Add focused failure tests**

Add `[Theory]`/`[Fact]` cases that assert failure for:

```csharp
new PdmCloneNode { NativeFileName = "../escape.ics" }
new PdmCloneNode { NativeFileName = "same.ics" } // used by two definitions
new PdmCloneNode { NativeFileName = "missing.ics" }
new PdmCloneBomEdge { ParentNodeId = "child", ChildNodeId = "root" } // cycle
```

Also assert the successful package contains no `ARAS01`, `.dwg`, `.pdf`, or `*-STRUCTURE.txt`.

- [ ] **Step 5: Run builder and existing manifest tests**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter "PdmClonePackageBuilderTests|PdmPackageImportReaderTests|PdmNormalizeExportSafetyTests"
```

Expected: all selected tests PASS.

- [ ] **Step 6: Commit the package builder**

```powershell
git add src/IdeaCadConnector.Workspace/Clone/PdmClonePackageBuilder.cs tests/IdeaCadConnector.Tests/PdmClonePackageBuilderTests.cs
git commit -m "feat(pdm): build clone packages in manifest v2 layout"
```

---

### Task 3: Rewire live Aras Clone to the package builder

**Files:**
- Modify: `src/IdeaCadConnector.Aras/IdeaCadConnector.Aras.csproj`
- Modify: `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:138-286,1210-1270,1510-1744`
- Modify: `src/IdeaCadConnector.Core/Contracts/IPdmRepositoryClient.cs:137-156`
- Test: `tests/IdeaCadConnector.Tests/PdmCloneRoundTripTests.cs`

**Interfaces:**
- Consumes: Task 1 `IVaultFileClient` and Task 2 `PdmClonePackageBuilder`.
- Produces: `PdmCloneResult.RootCadFilePath` and `PlaceholderDocumentCount == 0`.

- [ ] **Step 1: Write a failing repository round-trip test**

Configure `CloneAmlClient` to return a root Part, one child Part, one `Part BOM` relationship with quantity `2`, one linked root CAD, and one linked child CAD. Configure `CloneVaultClient` to write the filename associated with each fake File id. Assert:

```csharp
var result = await client.CloneLatestToWorkspaceAsync(new PdmCloneRequest
{
    RepositoryCode = "PDM-STUDYCASE",
    TargetFolder = folder.Path,
    BranchName = "main"
}, CancellationToken.None);

Assert.True(result.Success, result.ErrorMessage);
Assert.Equal(Path.Combine(folder.Path, "cad"), result.ResolvedCadFolder);
Assert.Equal(Path.Combine(folder.Path, "cad", "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), result.RootCadFilePath);
Assert.Equal(2, result.DownloadedCadFileCount);
Assert.Equal(0, result.PlaceholderDocumentCount);
Assert.False(Directory.Exists(Path.Combine(folder.Path, "ARAS01")));
Assert.True(File.Exists(Path.Combine(folder.Path, "pdm-bom-manifest.json")));
```

- [ ] **Step 2: Run the round-trip test and verify the legacy-layout failure**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter PdmCloneRoundTripTests
```

Expected: FAIL because `ResolvedCadFolder` is currently the project root, `RootCadFilePath` is absent, and Clone creates `ARAS01`/placeholders.

- [ ] **Step 3: Preserve BOM relationship data**

Replace `GetChildPartIdsAsync` with:

```csharp
private async Task<IReadOnlyList<CloneBomEdge>> GetChildPartEdgesAsync(string parentPartId, CancellationToken ct)
```

Query:

```xml
<Item type="Part BOM" action="get" select="related_id,quantity,sort_order">
  <source_id>...</source_id>
</Item>
```

Map missing/invalid quantity to `1` and missing sort order to response order `(index + 1) * 10`. Keep every relationship edge even when its child Part was already loaded, so repeated occurrences are not lost.

- [ ] **Step 4: Download into an external temporary package and build the V2 package**

Replace the legacy directory setup with:

```csharp
var projectFolder = Path.GetFullPath(request.TargetFolder.Trim());
var tempRoot = Path.Combine(Path.GetTempPath(), "IdeaPdmClone", Guid.NewGuid().ToString("N"));
var tempCadFolder = Path.Combine(tempRoot, "cad");
Directory.CreateDirectory(tempCadFolder);
```

For each selected CAD, require `native_file`, call `_vault.DownloadFileAsync`, verify the returned file is inside `tempCadFolder`, ends in `.ics`, and matches the canonical native name. Collect `PdmCloneNode` and `PdmCloneBomEdge`, call `PdmClonePackageBuilder.Build`, then publish `.idea-pdm`, `cad`, and `pdm-bom-manifest.json` into `projectFolder`. Delete `tempRoot` in `finally`.

Remove calls to `GetRelatedDocumentNamesAsync`, `EnsurePlaceholderFile`, and `GeneratePackageShapeFiles` from Clone. Delete legacy helper methods only after `rg` confirms no remaining callers.

- [ ] **Step 5: Make failure atomic**

Before publication, reject a destination containing any conflicting package path. During publication, track each moved top-level path and remove only paths created by this Clone if a later move fails. Never recursively delete `projectFolder` itself.

Return failure when any required CAD native file is absent or package validation fails:

```csharp
return new PdmCloneResult
{
    Success = false,
    RepositoryCode = repositoryCode,
    ResolvedProjectFolder = projectFolder,
    ResolvedCadFolder = Path.Combine(projectFolder, "cad"),
    PlaceholderDocumentCount = 0,
    ErrorMessage = message,
    Warnings = warnings
};
```

- [ ] **Step 6: Add the result contract property and Workspace reference**

```csharp
public string RootCadFilePath { get; set; }
```

Add to `IdeaCadConnector.Aras.csproj`:

```xml
<ProjectReference Include="..\IdeaCadConnector.Workspace\IdeaCadConnector.Workspace.csproj" />
```

- [ ] **Step 7: Add failure-path tests**

Add tests for missing root native file, missing child native file, vault download exception, unsafe returned filename, duplicate native filename, invalid BOM cycle, and a destination with existing `cad`. For each, assert `Success == false`, no `pdm-bom-manifest.json`, no partial `cad`, and no `.pending-*` directory under the user destination.

- [ ] **Step 8: Run repository Clone tests**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter PdmCloneRoundTripTests
```

Expected: all Clone round-trip and failure tests PASS.

- [ ] **Step 9: Commit the repository integration**

```powershell
git add src/IdeaCadConnector.Aras/IdeaCadConnector.Aras.csproj src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs src/IdeaCadConnector.Core/Contracts/IPdmRepositoryClient.cs tests/IdeaCadConnector.Tests/PdmCloneRoundTripTests.cs
git commit -m "fix(pdm): clone native files into pushed package layout"
```

---

### Task 4: Align the desktop Clone workflow

**Files:**
- Modify: `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:1058-1125`
- Test: `tests/IdeaCadConnector.Tests/PdmProjectsManifestIntegrationTests.cs`

**Interfaces:**
- Consumes: Task 3 `PdmCloneResult.RootCadFilePath`, normalized package root, and generated branch registry.
- Produces: analysis of the cloned manifest without deleting Clone-created metadata.

- [ ] **Step 1: Write the failing ViewModel success test**

Use a stub `IPdmRepositoryClient` returning a successful result with a populated `RootCadFilePath`. Assert the ViewModel sets `FolderPath`, analyzes the manifest package, and does not call behavior that deletes `.idea-pdm/workspace.json` or rewrites branches unnecessarily.

```csharp
Assert.Equal(cloneRoot, viewModel.FolderPath);
Assert.Equal("main", viewModel.SelectedBranch);
Assert.Contains("2", viewModel.StatusMessage);
Assert.DoesNotContain("placeholder", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run the focused ViewModel test and verify it fails**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter "Clone*PdmProjects*"
```

Expected: FAIL because the current success path clears workspace metadata and formats a placeholder count.

- [ ] **Step 3: Remove legacy post-Clone mutations and summary**

After success:

```csharp
FolderPath = result.ResolvedProjectFolder ?? targetFolder;
LoadBranchesForFolder();
SelectedBranch = Branches.Contains(selectedBranch) ? selectedBranch : "main";
AnalyzeFolder();
StatusMessage = $"Clone complete: {result.DownloadedCadFileCount} native CAD files, package {FolderPath}.";
```

Do not call `ClearManifest`, `EnsureMainBranch`, or `EnsureLocalBranchExists`; the validated package builder owns `.idea-pdm/branches.json`. Keep warnings appended to the success status.

- [ ] **Step 4: Run focused ViewModel tests**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 5: Commit the desktop alignment**

```powershell
git add src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs tests/IdeaCadConnector.Tests
git commit -m "fix(pdm): analyze cloned manifest package directly"
```

---

### Task 5: Full verification and runtime handoff

**Files:**
- Verify only; update code/tests only if a failure is directly caused by Tasks 1-4.

**Interfaces:**
- Consumes: completed Clone round-trip implementation.
- Produces: Debug and Release test evidence plus a runnable Debug build.

- [ ] **Step 1: Run formatting and repository cleanliness checks**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only intentional implementation changes before their final commit.

- [ ] **Step 2: Run the full Debug suite**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --no-restore
```

Expected: all tests PASS, including Clone, Push, manifest, library, and IronCAD integration unit tests.

- [ ] **Step 3: Run the full Release suite**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Release --no-restore
```

Expected: all tests PASS.

- [ ] **Step 4: Build the desktop app for runtime testing**

```powershell
dotnet build src/IdeaCadConnector.Desktop/IdeaCadConnector.Desktop.csproj -c Debug --no-restore
```

Expected: build succeeds with zero errors.

- [ ] **Step 5: Verify the real output shape after the user runs Clone**

Expected filesystem:

```text
PDM-STUDYCASE/
|-- .idea-pdm/branches.json
|-- cad/                         # 88 .ics files for the current StudyCase
`-- pdm-bom-manifest.json        # 88 definitions, 88 occurrences, 87 BOM edges
```

Open the manifest `RootFile` under `cad/` in IronCAD and confirm the assembly resolves from the cloned package. Confirm there is no `ARAS01`, placeholder PDF/DWG, `STRUCTURE.txt`, or visible `.pending-*` folder.

- [ ] **Step 6: Commit any final test-only adjustment and report evidence**

```powershell
git status -sb
git log -6 --oneline
```

Expected: working tree clean; report exact Debug/Release passed counts and the Debug executable path. Do not claim IronCAD runtime success until the user performs the real Clone/open check.
