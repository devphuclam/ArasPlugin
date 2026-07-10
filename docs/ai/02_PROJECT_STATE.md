# 02 — Project State

_Last audit source: uploaded repository archive, 2026-07-10._

## Repository

- Solution: `IdeaCadConnector.sln`
- Framework: .NET Framework 4.8
- Desktop: WPF/MVVM
- CAD focus: IronCAD `.ics`
- Aras communication: AML/HTTP/Vault and server-side C# Methods
- Snapshot commit observed in archive: `29e2ef364eaa3106c3fba76a3da5b0d5bcdf0eba`
- Current baseline commit (HEAD): `443847ff577710705879bbdd06013e1b58f1af03` (includes AI Work Kit + OpenCode + PROJECT_STATE.md update)
- Clean source baseline tag: `baseline/clean-source` -> `1c8a1b99672f5c791aa299dbebd70360503a71c3` (source code without AI infrastructure)
- AI Work Kit baseline tag: `baseline/with-ai-work-kit` -> `443847f` (HEAD including all AI governance files)

## Build Environment

- SDK: .NET SDK 10.0.300 (runtime 10.0.8)
- OS: Windows
- Target framework: net48 (via `Directory.Build.props`)
- Strong-name signing: enabled (uses `ICApiAddin.snk`)
- VS2022/MSBuild at standard path: not found (not required; `dotnet build` is used)
- `scripts/build-solution.ps1`: does not exist (use `dotnet build` directly --- see Testing Guide)

## Important warning --- resolved by BASE-00

BASE-00 established a clean, backed-up baseline. The original dirty working tree from the uploaded archive has been documented and isolated. Working tree is now clean. Do not assume any pre-BASE-00 uncommitted changes are intentional or safe.

## Confirmed existing capabilities

- Login/logout and Part search.
- Checkout, read-only open, Check-in and cancel checkout.
- CAD native-file Vault upload/download.
- CAD lifecycle/workflow actions.
- PDM folder analyze and push preview.
- Push Part, BOM, CAD and Document metadata.
- Clone Part/BOM and real CAD file.
- Document clone currently creates zero-byte placeholders.
- Local branches and non-main staging behavior.
- PDM Commit best-effort support.
- Start New Revision client and `idea_ReviseCad` reference server method.
- Part Library creation, management, revision policy, reuse and usage tracking.

## Confirmed gaps targeted by this roadmap

- Pull command is not connected to real synchronization.
- Physical Document file upload/attachment/download is incomplete.
- Commit file history lacks complete per-file diff/author/parent/Vault linkage.
- Branch is not a complete server-side snapshot branch model.
- Several sidebar destinations/settings/reports remain incomplete.

## Current execution phase

- Phase: Baseline and schema verification.
- Current ticket: `BASE-05` has live server-method inventory evidence only; Dev/Test method inventory remains unverified because no Dev/Test endpoint/token was provided.
- Next allowed work: reconcile source/live differences for live methods, collect Dev/Test method inventory, or decode live permissions/lifecycle.
- Do not begin: `DOC-03`, Pull, Branch, or server-side Branch/Commit schema work until Document File write behavior, permissions, lifecycle paths, and required PDM schema are confirmed.

## Update discipline

The Verifier updates this document after each merged ticket:

- current main commit;
- completed ticket;
- test baseline;
- newly confirmed schema;
- known blocker;
- next allowed tickets.

## BASE-00 completion record

- Completed: 2026-07-10
- HEAD commit: `443847ff577710705879bbdd06013e1b58f1af03`
- Build: Succeeded (0 warnings, 0 errors)
- Test: 419 passed, 0 failed, 0 skipped (4 s)
- Tags created: `baseline/clean-source`, `baseline/with-ai-work-kit`
- Working tree: clean
- Next ticket: `BASE-01` (build baseline)

## BASE-01 completion record

- Completed: 2026-07-10
- HEAD commit: `49d721d473fefdb2b5b0d22df9375d65713f7628`
- Build Debug: Succeeded (0 warnings, 0 errors, 11.31 s)
- Build Release: Succeeded (0 warnings, 0 errors, 7.31 s)
- All 7 projects in solution built in both Debug and Release
- 2 csproj files outside solution documented (OcrTool, CreateIronCadTestFiles)
- Working tree: clean
- Next ticket: `BASE-02` (test baseline)
## BASE-02 completion record

- Completed: 2026-07-10
- HEAD commit: `329e8b88082d7b493fafe11e2a3e1f85df5541bb`
- Test: 419 passed, 0 failed, 0 skipped (9.99 s)
- Detailed output: `.ai-work/verification/BASE-02-test-detailed-output.log`
- All tests run without live Aras (mocks/fakes used throughout)
- No test categories or traits modified
- Test file breakdown:

  | File | Tests | Subject Area |
  |---|---|---|
  | PartLibraryStage2Tests.cs | 91 | Part Library Stage 2 |
  | PartLibraryStage1Tests.cs | 79 | Part Library Stage 1 |
  | PartLibraryTests.cs | 44 | Part Lib policies, AML, errors |
  | PartLibraryVaultServiceTests.cs | 33 | Vault cache, download |
  | LibraryViewModelTests.cs | 26 | Library ViewModel |
  | ArasOpenUrlServiceTests.cs | 25 | Open-in-Aras URL |
  | LibraryManagementUiTests.cs | 25 | Management UI/XAML |
  | PartLibraryStage2CoreTests.cs | 17 | Part Lib Stage 2 Core |
  | EnvironmentConfigurationTests.cs | 16 | Env config parsing |
  | IronCadOpenServiceTests.cs | 15 | IronCAD adapter |
  | PartRevisionBrowserViewModelTests.cs | 14 | Revision browser |
  | ArasPartPickerViewModelTests.cs | 11 | Part picker |
  | LibraryAuthorizationServiceTests.cs | 10 | Authorization |
  | MoveLibraryEntryViewModelTests.cs | 8 | Move entry dialog |
  | BrowserLauncherTests.cs | 4 | Browser launch |
  | LibraryLocalizationTests.cs | 1 | Localization |
  | **Total** | **419** | |

- Working tree: clean
- Next ticket: `BASE-03` (install AI governance workflow)

## BASE-03 completion record

- Completed: 2026-07-10
- HEAD commit: `a7ccab3bdf2294f818e70ac1f0eda606945f9e1a`
- Work kit files: 100% tracked (docs/ai/ = 25 files, scripts/ai/ = 7, tasks/ai/ = 50+ tickets + backlog, .opencode/ = 10, .github/ = 3, root docs = 4)
- GitIgnore unignore rules: confirmed correct (both AI WORK KIT and OPENCODE AI WORK KIT blocks)
- Start/Verify scripts: syntax verified (Start-AiTicket, Verify-AiTicket, Initialize-AiWorkKit, Check-AiScope, Collect-AiContext)
- No application source modified
- Working tree: clean
- Next ticket: `BASE-04` (verify Aras schema map)

## BASE-04 blocker record

- Started: 2026-07-10
- HEAD commit at planning start: `fe99d2a39a364545a01b5909a6967427cd9eff56`
- Scope: verify Aras schema map; do not create schema
- Result so far: partially verified against live Aras; remaining blockers documented
- Build Debug: Succeeded (0 warnings, 0 errors)
- Build Release: Succeeded (0 warnings, 0 errors)
- Test Debug: 419 passed, 0 failed, 0 skipped
- Application source changes: none
- Schema changes: none
- Live confirmed:
  - core ItemTypes and relationships currently used by clone/push;
  - `Document File` relationship exists and links `Document` to `File`;
  - Part Library ItemTypes and key properties exist;
  - all eight listed server methods exist on live.
- Live confirmed absent:
  - `PDM Commit`;
  - `PDM Commit File`;
  - `PDM Branch`.
- Still blocked:
  - Document File write/version/permission behavior;
  - role-specific permission matrix;
  - lifecycle transition map details;
  - any ticket that requires server-side PDM Branch/Commit schema.

## BASE-05 live inventory record

- Live inventory completed: 2026-07-10
- HEAD commit at implementation start: `bda183fbf4952bb697f0dcdb1994d78339371eff`
- Scope: inventory deployed server methods; no live deploy or schema change
- Live target: `<innovator-server-url>`, database `<db-name>`
- Evidence: `.ai-work/verification/BASE-05-server-methods/method-inventory.md`
- Build/test evidence: `.ai-work/verification/BASE-05-server-methods/BASE-05-build-debug.log`, `.ai-work/verification/BASE-05-server-methods/BASE-05-build-release.log`, `.ai-work/verification/BASE-05-server-methods/BASE-05-test-debug.trx`
- Secret handling: token read from ignored `.ai-work/live-token.local.txt`; token value was not recorded in evidence or docs
- Live method presence: 8/8 source server methods confirmed live
- Source/live parity:
  - MATCH: `idea_ReviseCad`, `idea_StartDetailedDesign`, `idea_RecordPartLibraryUsage`, `idea_GetPrimaryIronCadForPart`, `idea_SyncPartLibraryEntryStatus`
  - DIFFERS: `idea_EnsurePrimaryIronCadPartCad`, `idea_CommitCadCheckin`, `idea_AddPartToLibrary`
- No application source changes
- No Aras production deploy
- Build Debug: Succeeded
- Build Release: Succeeded
- Test Debug: 419 passed, 0 failed, 0 skipped
- BASE-05 status: partially completed; Dev/Test environment method inventory remains unverified.
- Next allowed work:
  - reconcile or intentionally redeploy the three differing server methods before depending on source/live parity;
  - collect Dev/Test method presence and version evidence when endpoints/tokens are provided;
  - continue decoding permission/lifecycle/Document File write behavior;
  - do not start server-side Branch/Commit tickets until `PDM Commit`, `PDM Commit File`, and `PDM Branch` schema is planned and deployed.
