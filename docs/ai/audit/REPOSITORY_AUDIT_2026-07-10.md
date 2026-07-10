# ArasPlugin / IdeaCadConnector Code Audit

Scope: `/mnt/data/CurrentProject.zip` → `ARAS-Plugin/IdeaCadConnector` tracked files. Excluded `.git`, `.vs`, `bin`, `obj`, generated build output, and binary files.

- Git-tracked files: 247
- Text/code/docs files read: 240
- Binary/non-text tracked files skipped: 7
- Total text lines read: 55,976
- C# classes/interfaces/enums/struct declarations found: 296
- xUnit `[Fact]`/`[Theory]` tests found: 399 across 16 files

## Binary / non-text tracked files skipped

- `ICApiAddin.snk`
- `docs/part-library/references/mockups/01_Library_Main.png`
- `docs/part-library/references/mockups/02_Save_To_Library_Dialog.png`
- `docs/part-library/references/mockups/03_Add_To_Project_Dialog.png`
- `docs/part-library/references/mockups/04_Publish_And_Revision_Dialog.png`
- `src/IdeaCadConnector.IronCAD/ICApiAddin.snk`
- `tools/CreateIronCadTestFiles/lib/Interop.ICApiIronCAD.dll`

## Projects

Solution includes:
- `src/IdeaCadConnector.Core/IdeaCadConnector.Core.csproj`
- `src/IdeaCadConnector.Aras/IdeaCadConnector.Aras.csproj`
- `src/IdeaCadConnector.Workspace/IdeaCadConnector.Workspace.csproj`
- `src/IdeaCadConnector.Ui/IdeaCadConnector.Ui.csproj`
- `src/IdeaCadConnector.IronCAD/IdeaCadConnector.IronCAD.csproj`
- `src/IdeaCadConnector.Desktop/IdeaCadConnector.Desktop.csproj`
- `tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj`

CSProj not included in solution:
- `src/IdeaCadConnector.OcrTool/IdeaCadConnector.OcrTool.csproj`
- `tools/CreateIronCadTestFiles/CreateIronCadTestFiles.csproj`

## Main findings

1. **README stale: Inventor/legacy architecture** — `README.md`  
   README still describes Inventor and idea_EnsurePrimaryInventorPartCad while actual server method set is IronCAD-oriented and includes idea_EnsurePrimaryIronCadPartCad/idea_ReviseCad.
2. **Pull not implemented** — `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:129`  
   PullCommand only sets “Pull is not connected to Aras yet”; no Pull/Sync flow exists.
3. **Sidebar buttons without handlers** — `src/IdeaCadConnector.Desktop/MainWindow.xaml:465-552`  
   Recent/Favorites/Projects/Reports/Settings/About buttons are visible but have no Click/Command; SetActiveNavigation only knows Home/Search/PDM/Library.
4. **Document files are metadata-only on push** — `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:912-977`  
   CreateOrGetDocumentAsync creates/reuses Document and relationship, but does not upload/attach the physical document file; classification is preview-only.
5. **Clone creates document placeholders** — `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:215-284`  
   Clone downloads CAD native files, but related Documents are zero-byte placeholder files.
6. **Branch model is local/staging only** — `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:168-329`  
   Non-main branch push does not update live Part/BOM/CAD/Document data; clone ignores server-side branch.
7. **PDM Commit detail incomplete** — `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1066-1168`  
   Commit author not sent; commit file vault_file_id missing; change_type hardcoded to added.
8. **Local workspace commit model is summary-only** — `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:13-31`  
   Local commits store only counts/signature, not per-file hash, author, parent commit, or file list.
9. **Workspace load errors swallowed** — `src/IdeaCadConnector.Workspace/WorkspaceService.cs:49-70,102-129,143-170`  
   Manifest/commit/branch load catches errors and returns null/default without logging.
10. **Publish dialog is visual placeholder/unused** — `src/IdeaCadConnector.Desktop/Dialogs/PublishLibraryEntryDialog.xaml:168-328`  
   Dialog says backend workflow is not wired; code search found no construction of PublishLibraryEntryDialog, while LibraryViewModel publishes directly.
11. **OcrTool and CreateIronCadTestFiles not in solution** — `IdeaCadConnector.sln`  
   There are 9 csproj files but only 7 in the solution; OcrTool and tools/CreateIronCadTestFiles are excluded.

## TODO / limitation evidence

### TODO/FIXME/NotImplemented (18 hits)

- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1066` — // TODO(PERM-COMMIT-AUTHOR): Add <author> field from session user.
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1160` — // TODO(PERM-COMMIT-FILE-VAULT): Add vault_file_id after file upload.
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1163` — // TODO(PERM-COMMIT-FILE-CHANGE-TYPE): Derive change_type from diff
- `src/IdeaCadConnector.Core/Cad/CadLifecyclePolicy.cs:131` — // TODO(PERF-REVISION-SEAM): Move into IRevisionService when
- `src/IdeaCadConnector.Core/Cad/CadLifecyclePolicy.cs:139` — // TODO(PERF-REVISION-SEAM): Extract into IRevisionService when
- `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:402` — // TODO(PERF-REVISION-SEAM): Wire real server path when PDM schema
- `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:3482` — // TODO(PERF-CONTENT-HASH): Replace with SHA256-based PdmContentHasher
- `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:3641` — // TODO(PERF-COMMIT-FILES): Add PdmCommitFileEntry collection when
- `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:15` — // TODO(PERF-COMMIT-FILES): Replace with List<PdmCommitFileEntry> when
- `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:22` — // TODO(PERF-CONTENT-HASH): Replace SnapshotSignature with SHA256-based
- `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:26` — // TODO(PERF-COMMIT-GRAPH): Add nullable ParentCommitId field when
- `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:28` — // TODO(PERF-COMMIT-FILES): Add List<PdmCommitFileEntry> Files when
- `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:30` — // TODO(PERF-COMMIT-AUTHOR): Add Author field when server-backed
- `src/IdeaCadConnector.Workspace/WorkspaceService.cs:106` — // TODO(PERF-INTERFACE): Extract IWorkspaceCommitStore from
- `src/IdeaCadConnector.Workspace/WorkspaceService.cs:108` — // TODO(PERF-ERROR-HANDLING): Add logging in catch blocks.
- `src/IdeaCadConnector.Workspace/WorkspaceService.cs:147` — // TODO(PERF-INTERFACE): Extract IWorkspaceBranchStore from
- `src/IdeaCadConnector.Workspace/WorkspaceService.cs:149` — // TODO(PERF-ERROR-HANDLING): Add logging in catch blocks.
- `tests/IdeaCadConnector.Tests/PartLibraryVaultServiceTests.cs:74` — public void ToCacheFileName_NullExtension_FallsBackToDotCache()

### Placeholder/Not wired (44 hits)

- `docs/part-library/phase-2/README.md:181` — **Detail tabs (WS7):** Backend DTOs + `IPartLibraryClient` methods (`GetCadDetailsAsync`, `GetBomDetailsAsync`, `GetRevisionDetailsAsync`, `GetWhereUsedDetailsAsync`, `GetDetailBundleAsync`) — `HttpPartLibraryClient` throws `NotSupportedException` as placeholder for Sprint 2.3 UI wiring.
- `docs/part-library/phase-3/DESIGN.md:47` — | `aras` | Base URL, database, Open-in-Aras URL | Not wired (prefill candidate for Sprint 3.3) |
- `docs/part-library/phase-3/DESIGN.md:48` — | `local` | Vault cache directory, IronCAD path, auto-open | Not wired (candidate for Sprint 3.3) |
- `docs/part-library/phase-3/DESIGN.md:50` — | `diagnostics` | Log level, file logging, log directory | Not wired (requires logging infrastructure) |
- `docs/part-library/phase-3/ENVIRONMENT-CONFIGURATION.md:121` — | `aras.baseUrl` | Not wired yet | Can prefill login dialog. Candidate for Sprint 3.3. |
- `docs/part-library/phase-3/ENVIRONMENT-CONFIGURATION.md:122` — | `aras.database` | Not wired yet | Can prefill login dialog. Candidate for Sprint 3.3. |
- `docs/part-library/phase-3/ENVIRONMENT-CONFIGURATION.md:123` — | `aras.openInArasBaseUrl` | Not wired yet | Candidate for Sprint 3.3. |
- `docs/part-library/phase-3/ENVIRONMENT-CONFIGURATION.md:124` — | `local.vaultCacheDirectory` | Not wired yet | PartLibraryVaultService has its own default. Candidate for Sprint 3.3. |
- `docs/part-library/phase-3/ENVIRONMENT-CONFIGURATION.md:125` — | `local.ironCadExecutablePath` | Not wired yet | AppSessionContext has IronCadExecutablePath. Candidate for Sprint 3.3. |
- `docs/part-library/phase-3/ENVIRONMENT-CONFIGURATION.md:126` — | `local.openDownloadedCadAfterDownload` | Not wired yet | Future candidate. |
- `docs/part-library/phase-3/ENVIRONMENT-CONFIGURATION.md:128` — | `diagnostics.*` | Not wired | Requires logging infrastructure. Future candidate. |
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:184` — var placeholderDocumentCount = 0;
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:225` — EnsurePlaceholderFile(targetPath);
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:226` — placeholderDocumentCount++;
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:258` — EnsurePlaceholderFile(targetPath);
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:259` — placeholderDocumentCount++;
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:264` — placeholderDocumentCount += GeneratePackageShapeFiles(
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:274` — Success = downloadedCadCount > 0 || placeholderDocumentCount > 0,
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:281` — PlaceholderDocumentCount = placeholderDocumentCount,
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:283` — ErrorMessage = downloadedCadCount == 0 && placeholderDocumentCount == 0
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:284` — ? "No CAD native files or related document placeholders could be cloned from Aras."
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1507` — private static void EnsurePlaceholderFile(string path)
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1556` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(projectFolder, projectCode + "_Ver" + version + ".dwg"));
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1565` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(drawingsFolder, drawingName));
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1581` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1602` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(projectFolder, pdfName));
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1655` — private static int EnsurePlaceholderFileCreated(string path)
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1660` — EnsurePlaceholderFile(path);
- `src/IdeaCadConnector.Core/Cad/CadConstants.cs:5` — //   workspace placeholder file before the server has returned a real
- `src/IdeaCadConnector.Core/Contracts/IPdmRepositoryClient.cs:149` — public int PlaceholderDocumentCount { get; set; }
- `src/IdeaCadConnector.Core/Localization/TranslationKeys.cs:724` — public const string PublishDialogPlaceholderText = "PublishDialogPlaceholderText";
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:712` — [TranslationKeys.PublishDialogWorkflowStatus] = "Publish workflow is prepared visually, but the Aras workflow backend is not wired in this MVP slice.",
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:713` — [TranslationKeys.PublishDialogNotWired] = "Not wired",
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:719` — [TranslationKeys.PublishDialogPlaceholderText] = "This placeholder keeps the Phase 2 UX honest: it shows the intended publish flow without creating unsupported server behavior.",
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:728` — [TranslationKeys.PublishDialogBackendUnavailable] = "Publish workflow backend is not available yet.",
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:852` — [TranslationKeys.PdmCloneComplete] = "Clone complete. Downloaded {0} CAD file(s) and created {1} document placeholder(s) in {2}.",
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:1493` — [TranslationKeys.PublishDialogPlaceholderText] = "Chỗ giữ chỗ này giữ cho UX Phase 2 trung thực: nó hiển thị luồng xuất bản dự kiến mà không tạo hành vi máy chủ không được hỗ trợ.",
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:2267` — [TranslationKeys.PublishDialogPlaceholderText] = "このプレースホルダーはPhase 2のUXを正直に保ちます。サポートされていないサーバー動作を作成せずに、意図された公開フローを表示します。",
- `src/IdeaCadConnector.Core/Validation/CadFileNamingRules.cs:10` — // The single helper kept here is for choosing a LOCAL placeholder filename
- `src/IdeaCadConnector.Core/Validation/CadFileNamingRules.cs:15` — public static string GetLocalPlaceholderFileName(string partNumber)
- `src/IdeaCadConnector.Desktop/Dialogs/PublishLibraryEntryDialog.xaml:244` — Text="{Binding Source={x:Static core:LocalizationSource.Instance}, Path=[PublishDialogPlaceholderText]}"
- `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:1112` — result.PlaceholderDocumentCount,
- `src/IdeaCadConnector.Workspace/WorkspaceService.cs:26` — var fileName = CadFileNamingRules.GetLocalPlaceholderFileName(partNumber);
- `tools/New-IronCadProject.ps1:11` — .PARAMETER SkipIcs        Skip IronCAD COM - only create PDF/DWG placeholder files.

### Local-only/branch limitation (5 hits)

- `docs/part-library/references/schemas/UI_State_Matrix.csv:9` — PartNotPushed,Selected PDM node has no Aras Part ID,Push the Part to Aras before saving,Open PDM Project,Save to Library,Do not create Library relationship to local-only node
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:174` — warnings.Add("Clone currently uses latest live data on Aras. Branch '" + request.BranchName + "' is local-only and was not resolved on server.");
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:305` — var stagingMsg = $"Non-main branch '{request.TargetBranch}': push created staging snapshot only. Live Part/BOM/CAD/Document data was not updated.";
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs:303` — [TranslationKeys.SaveToLibraryPartIdRequirementHint] = "Part ID is required. Local-only nodes must be pushed to Aras before saving to Library.",
- `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:31` — // commits are introduced. Currently local-only, no author tracking.

### Commit limitations (11 hits)

- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:323` — _logger.LogWarning(ex, "PDM Commit schema unavailable. Staging snapshot skipped.");
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:328` — result.Warnings = new[] { stagingMsg, "PDM Commit schema unavailable. Staging snapshot skipped." };
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:506` — _logger.LogWarning(ex, "PDM Commit schema unavailable. Business push completed without commit snapshot.");
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1066` — // TODO(PERM-COMMIT-AUTHOR): Add <author> field from session user.
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1067` — // Currently not sent; server field exists but client never populates it.
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1160` — // TODO(PERM-COMMIT-FILE-VAULT): Add vault_file_id after file upload.
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1162` — // download. Currently not sent.
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1163` — // TODO(PERM-COMMIT-FILE-CHANGE-TYPE): Derive change_type from diff
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1171` — $"<change_type>added</change_type>" +
- `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:3482` — // TODO(PERF-CONTENT-HASH): Replace with SHA256-based PdmContentHasher
- `src/IdeaCadConnector.Workspace/WorkspaceCommit.cs:22` — // TODO(PERF-CONTENT-HASH): Replace SnapshotSignature with SHA256-based

### Document limitations (20 hits)

- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:184` — var placeholderDocumentCount = 0;
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:225` — EnsurePlaceholderFile(targetPath);
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:226` — placeholderDocumentCount++;
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:258` — EnsurePlaceholderFile(targetPath);
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:259` — placeholderDocumentCount++;
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:264` — placeholderDocumentCount += GeneratePackageShapeFiles(
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:274` — Success = downloadedCadCount > 0 || placeholderDocumentCount > 0,
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:281` — PlaceholderDocumentCount = placeholderDocumentCount,
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:283` — ErrorMessage = downloadedCadCount == 0 && placeholderDocumentCount == 0
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:959` — ? $"Document add failed. number='{doc.DocumentNumber}', classification='{doc.Classification} (preview-only, not sent to Aras)', source='{doc.SourceFileName}'. Aras returned no id."
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:970` — ErrorMessage = $"Document add failed. number='{doc.DocumentNumber}', classification='{doc.Classification} (preview-only, not sent to Aras)', source='{doc.SourceFileName}'. Aras said: {ex.Message}"
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1507` — private static void EnsurePlaceholderFile(string path)
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1556` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(projectFolder, projectCode + "_Ver" + version + ".dwg"));
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1565` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(drawingsFolder, drawingName));
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1581` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1602` — createdCount += EnsurePlaceholderFileCreated(Path.Combine(projectFolder, pdfName));
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1655` — private static int EnsurePlaceholderFileCreated(string path)
- `src/IdeaCadConnector.Aras/HttpPdmRepositoryClient.cs:1660` — EnsurePlaceholderFile(path);
- `src/IdeaCadConnector.Core/Contracts/IPdmRepositoryClient.cs:149` — public int PlaceholderDocumentCount { get; set; }
- `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs:1112` — result.PlaceholderDocumentCount,

## Test files

- `tests/IdeaCadConnector.Tests/ArasOpenUrlServiceTests.cs` — 25 tests
- `tests/IdeaCadConnector.Tests/ArasPartPickerViewModelTests.cs` — 11 tests
- `tests/IdeaCadConnector.Tests/BrowserLauncherTests.cs` — 4 tests
- `tests/IdeaCadConnector.Tests/EnvironmentConfigurationTests.cs` — 16 tests
- `tests/IdeaCadConnector.Tests/IronCadOpenServiceTests.cs` — 15 tests
- `tests/IdeaCadConnector.Tests/LibraryAuthorizationServiceTests.cs` — 3 tests
- `tests/IdeaCadConnector.Tests/LibraryLocalizationTests.cs` — 1 tests
- `tests/IdeaCadConnector.Tests/LibraryManagementUiTests.cs` — 25 tests
- `tests/IdeaCadConnector.Tests/LibraryViewModelTests.cs` — 26 tests
- `tests/IdeaCadConnector.Tests/MoveLibraryEntryViewModelTests.cs` — 8 tests
- `tests/IdeaCadConnector.Tests/PartLibraryStage1Tests.cs` — 71 tests
- `tests/IdeaCadConnector.Tests/PartLibraryStage2CoreTests.cs` — 14 tests
- `tests/IdeaCadConnector.Tests/PartLibraryStage2Tests.cs` — 91 tests
- `tests/IdeaCadConnector.Tests/PartLibraryTests.cs` — 42 tests
- `tests/IdeaCadConnector.Tests/PartLibraryVaultServiceTests.cs` — 33 tests
- `tests/IdeaCadConnector.Tests/PartRevisionBrowserViewModelTests.cs` — 14 tests
