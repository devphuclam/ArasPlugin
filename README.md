# Idea CAD Connector — Project Structure & PDM System

```
IdeaCadConnector/
├── README.md
├── IdeaCadConnector.sln
├── lib/                                    # External dependencies (IOM.dll)
├── docs/
│   └── core/
│       └── IDEA-PDM-DESIGN-MASTER.md       # PDM task breakdown & architecture
├── src/
│   ├── IdeaCadConnector.Core/              # Shared contracts & DTOs
│   │   ├── Cad/
│   │   │   ├── CadConstants.cs             # Inventor authoring tool, classification, extension
│   │   │   └── CadOpenMode.cs              # Edit / ReadOnly enum
│   │   ├── Contracts/
│   │   │   ├── IArasCadClient.cs           # Interface: 8 async methods
│   │   │   ├── ICadApplicationAdapter.cs   # CAD application abstraction
│   │   │   └── IPdmRepositoryClient.cs     # PDM push interface + DTOs (PdmPushRequest, etc.)
│   │   ├── Dto/                            # DTOs
│   │   ├── Errors/
│   │   │   ├── ArasErrorCode.cs            # 13 typed error codes
│   │   │   └── ArasOperationException.cs   # Exception with ErrorCode + Retryable + Details
│   │   └── Validation/
│   │       └── CadFileNamingRules.cs
│   │
│   ├── IdeaCadConnector.Aras/              # Server communication (IOM + OData + REST)
│   │   ├── ArasAmlClient.cs               # AML apply abstraction
│   │   ├── ArasAuthenticator.cs            # IOM PasswordTokenProvider + OData token
│   │   ├── ArasCadClient.cs                # Full IArasCadClient implementation
│   │   ├── ArasClientOptions.cs            # Server URL, database, timeout, vault
│   │   ├── ArasHttpClient.cs              # HTTP GET/POST with Bearer token
│   │   ├── ArasSoapClient.cs              # SOAP abstraction
│   │   ├── HttpArasCadClient.cs           # HTTP-based CAD client
│   │   ├── HttpPdmRepositoryClient.cs     # PDM push: Part, CAD, Document, BOM, Commit
│   │   ├── PartSearchClient.cs             # OData $filter / $expand Part search
│   │   ├── VaultClient.cs                 # File upload/download to Aras Vault
│   │   ├── ServerMethods/                  # C# methods deployed to Aras server
│   │   │   ├── idea_EnsurePrimaryInventorPartCad.cs
│   │   │   └── idea_CommitCadCheckin.cs
│   │   └── IdeaCadConnector.Aras.csproj
│   │
│   ├── IdeaCadConnector.Workspace/         # Local PDM workspace management
│   │   ├── PdmNamingPolicy.cs             # ARAS01 naming rules + folder/package parsers
│   │   ├── PdmPushContracts.cs            # Push DTOs (AnalyzeResult, PushPreview, etc.)
│   │   ├── PdmPushPreviewBuilder.cs       # AnalyzeResult → PushPreview builder
│   │   ├── PushPreviewMapper.cs           # Folder + business → AnalyzeResult mapper
│   │   ├── WorkspaceService.cs            # Local file layout
│   │   ├── WorkspaceOptions.cs
│   │   └── IdeaCadConnector.Workspace.csproj
│   │
│   ├── IdeaCadConnector.Desktop/           # WPF desktop application
│   │   ├── MainWindow.xaml / .cs           # Main window (PDM + CAD tabs)
│   │   ├── MainViewModel.cs               # Shared session/state
│   │   ├── PdmProjectsView.xaml / .cs      # PDM tab: Structure/Changes/Naming/Preview
│   │   ├── PdmProjectsViewModel.cs         # PDM tab logic (analyze, push, structure)
│   │   ├── pdm-naming-policy.json          # Naming policy config
│   │   ├── RelayCommand.cs                # ICommand helper
│   │   ├── IronCadExternalAdapter.cs
│   │   └── IdeaCadConnector.Desktop.csproj
│   │
│   ├── IdeaCadConnector.Ui/                # Shared WPF views
│   │   ├── Views/
│   │   │   ├── LoginDialog.xaml / .cs
│   │   │   └── ...
│   │   ├── ViewModels/
│   │   │   └── LoginViewModel.cs
│   │   └── IdeaCadConnector.Ui.csproj
│   │
│   ├── IdeaCadConnector.Inventor/          # Inventor ApplicationAddInServer
│   │   ├── StandardAddInServer.cs          # 6 ribbon buttons, button handlers
│   │   ├── InventorCadAdapter.cs           # Read/write Inventor property sets
│   │   ├── IdeaCadConnector.Inventor.addin
│   │   └── IdeaCadConnector.Inventor.csproj
│   │
│   ├── IdeaCadConnector.IronCAD/           # IronCAD integration
│   │   └── IdeaCadConnector.IronCAD.csproj
│   │
│   ├── IdeaCadConnector.Cad/               # CAD abstraction layer
│   │   └── IdeaCadConnector.Cad.csproj
│   │
│   ├── IdeaCadConnector.ConsoleHarness/    # Console test harness
│   ├── IdeaCadConnector.LoginTester/       # Login testing tool
│   └── IdeaCadConnector.OcrTool/           # OCR utility
│
├── tools/                                  # Build/utility tools
└── screenshot/                              # App screenshots
```

## Architecture: Hybrid IOM + OData + PDM

```
┌─────────────────────────────────────────────────────────────────┐
│                    StandardAddInServer                          │
│              (Inventor ribbon buttons)                          │
└──────────┬──────────────────────────────────────┬───────────────┘
           │ uses                                 │ uses
           ▼                                      ▼
┌──────────────────────┐          ┌──────────────────────────────┐
│   ArasCadClient      │          │  InventorCadAdapter          │
│  (IArasCadClient)    │          │  (ICadApplicationAdapter)   │
│                      │          └──────────────────────────────┘
│  ┌────────────────┐  │
│  │ArasAuthenticator│ │  IOM.dll → Innovator instance
│  └────────────────┘  │
│  ┌────────────────┐  │
│  │PartSearchClient │  │  OData → server/odata/Part
│  └────────────────┘  │
│  IOM operations:     │
│  • inn.applyMethod() │  → idea_EnsurePrimaryInventorPartCad
│  • inn.applyMethod() │  → idea_CommitCadCheckin
│  • inn.newItem()     │  → lock / unlock / get CAD
│  • attachPhysicalFile│  → file upload (Vault)
│  • fetchFileProperty │  → file download (Vault)
└──────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    PDM System (Desktop app)                     │
│                                                                 │
│  ┌─────────────────┐   ┌──────────────────┐   ┌──────────────┐ │
│  │ Workspace       │   │  Workspace       │   │ Aras PDM     │ │
│  │ Index Manager   │──▶│  Diff Engine     │──▶│ Client       │ │
│  │ (index.json)    │   │ (change set)     │   │ (Push/Clone) │ │
│  └─────────────────┘   └──────────────────┘   └──────┬───────┘ │
│                                                       │         │
│  ┌─────────────────┐   ┌──────────────────┐           │         │
│  │ Naming Policy   │   │ Push Preview     │           │         │
│  │ Parser          │──▶│ Builder          │───────────┘         │
│  └─────────────────┘   └──────────────────┘                     │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  PdmProjectsViewModel  ◀──▶  PdmProjectsView (WPF)       │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### ArasCadClient Method → IOM Mapping

| Interface Method | IOM Implementation |
|---|---|
| `LoginAsync` | `PasswordTokenProvider` + `IomFactory.CreateHttpServerConnection()` + `connection.Login()` |
| `SearchPartsAsync` | Delegated to `PartSearchClient` (OData HTTP GET) |
| `CreateInventorPartCadAsync` | `inn.applyMethod("idea_EnsurePrimaryInventorPartCad", aml)` |
| `CheckoutAsync` | `inn.newItem("CAD", "lock")` → `inn.newItem("CAD", "get")` |
| `OpenReadOnlyAsync` | `inn.newItem("CAD", "get")` (no lock) |
| `UploadFileAsync` | `inn.newItem("File", "add")` → `attachPhysicalFile(path)` (via VaultClient) |
| `CheckinAsync` | `inn.applyMethod("idea_CommitCadCheckin", aml)` |
| `CancelCheckoutAsync` | `inn.newItem("CAD", "unlock")` |
| `DownloadNativeFileAsync` | `inn.newItem("File", "get")` → `fetchFileProperty("file_body", path)` |

### PDM Push Flow

```
Analyze Folder → Build Preview → Push to Aras
        │              │               │
        ▼              ▼               ▼
[Phase 1] plan    PushPreview DTOs   HttpPdmRepositoryClient
SHA256 hashes     PdmPushPreview     1. Create/Get Project
PdmIndexManager   Builder            2. Create/Get Parts + BOM
                                     3. Create/Get CADs (→ Vault upload — Phase 2)
                                     4. Create/Get Documents
                                     5. Create PDM Commit (try-catch — Phase 3)
                                     6. Update workspace index (Phase 2)
```

## Server Methods

Two C# methods run on the Aras Innovator server, deployed as Aras Method items:

- **`idea_EnsurePrimaryInventorPartCad`** — Idempotent creation/lookup of a primary
  Inventor Part CAD. Accepts `part_id`, returns CAD summary properties.
  Business rules: `item_number = PartNumber-IPT`, `classification = Mechanical/Part`,
  `authoring_tool = Inventor`.

- **`idea_CommitCadCheckin`** — Atomic check-in: validates lock ownership, attaches
  the uploaded native file, unlocks the CAD. Accepts `cad_id`, `uploaded_file_id`,
  optional `comment`.

The source files in `src/IdeaCadConnector.Aras/ServerMethods/` are reference copies.
To deploy: copy the code into an Aras Method item via the Aras admin UI.

## IOM.dll Dependency

`IOM.dll` is **not on NuGet**. It ships with Aras Innovator (under the server's
`Client/` folder or the Innovator Client installation). Used by `ArasCadClient`
for legacy IOM operations (lock/unlock, applyMethod).

The `.csproj` expects it at:

```
src/IdeaCadConnector.Aras/lib/IOM.dll
```

For OData/REST operations (Part search, PDM push, Vault upload), the system uses
`ArasHttpClient` with Bearer token auth — no IOM.dll required.

## Design Decisions

1. **Why IOM for some operations?** IOM is the official Aras .NET SDK for SOAP operations
   (lock/unlock, apply server methods). The PDM system uses HTTP (AML/OData) with Bearer
   tokens instead — avoiding IOM.dll dependency for the PDM push flow.

2. **Why keep OData for search?** AML's search/filter capabilities are more limited than
   OData `$filter`/`$expand`. The Part search with OData is a single HTTP GET; keeping it
   over REST avoids AML complexity and makes response parsing trivial with `System.Text.Json`.

3. **Why not full IOM?** The `Part` OData endpoint has `$expand=Part_CAD` which returns
   the linked CAD items in one round trip. AML would require multiple `apply()` calls,
   making search slower and more complex.

4. **No refresh-token logic.** The hybrid client does not implement OAuth refresh-token
   grants. If the IOM session or OData token expires, the user re-logs in. This simplifies
   the code and avoids edge cases where refresh succeeds but the IOM session has expired.

5. **1 .ics file = 1 CAD record** (IronCAD scene model). Unlike Inventor where each part
   file is a separate CAD, IronCAD stores the entire assembly in a single .ics file.
   The push preview builder generates exactly one CAD record per .ics.

6. **SHA256 for change detection.** Fast, collision-resistant, built into .NET. Stored
   per-file in `.idea-pdm/index.json`. Compared against last-push index to produce
   Added/Modified/Deleted change set.

7. **try-catch on PDM Commit creation.** The PDM schema (PDM Commit, Commit File, etc.)
   is not yet deployed on the Aras server. The business push (Part, CAD, Document, BOM)
   succeeds regardless; the commit snapshot is best-effort.

8. **Product Structure from naming analysis, not .ics parsing.** The Part tree is derived
   from package PDF/DWG naming conventions via `Aras01FolderAnalyzer` and
   `StudyCase0603StructureParser`. .ics files are treated as opaque CAD containers.
