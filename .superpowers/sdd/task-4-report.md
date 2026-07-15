# Task 4 report: Align the desktop Clone workflow

## Scope

- Updated `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs` only in the Clone success path.
- Added the focused regression test in `tests/IdeaCadConnector.Tests/PdmProjectsManifestIntegrationTests.cs`.
- No Aras, Core, or Workspace files were modified.

## RED evidence

Command:

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj -c Debug --filter "FullyQualifiedName~PdmProjectsManifestIntegrationTests"
```

Result before the production change: failed as expected.

- Failed: `CloneCommand_AnalyzesPublishedPackageWithoutDeletingMetadata`
- Failure: status contained `99 document placeholder(s)`.
- Passed: 1 existing manifest integration test.

## GREEN evidence

The same focused command after the change passed:

```text
Passed: 2, Failed: 0, Skipped: 0, Total: 2
```

The regression test verifies that Clone:

- keeps the resolved package folder as `FolderPath`;
- selects the generated `main` branch;
- analyzes the manifest-v2 package;
- preserves `.idea-pdm/workspace.json` and `.idea-pdm/branches.json`;
- reports the native CAD count and package path without placeholder wording;
- appends Clone warnings.

## Full Debug evidence

Command:

```powershell
dotnet test -c Debug
```

Result:

```text
Passed: 610, Failed: 0, Skipped: 0, Total: 610
```

The baseline was 609 tests; the additional passing test is the Task 4 regression test.

## Implementation notes

Removed the post-Clone calls to `ClearManifest`, `EnsureMainBranch`, and `EnsureLocalBranchExists`. The success path now reloads the builder-owned branch registry, selects the requested branch when present (otherwise `main`), analyzes the package, and reports:

```text
Clone complete: <native CAD count> native CAD files, package <FolderPath>.
```

Warnings remain appended to that status. No IronCAD launch was added.

## Concern

The brief's suggested filter `Clone*PdmProjects*` matched zero tests because the test method name does not contain `PdmProjects` after `Clone`; the exact class filter above was used for focused evidence.
