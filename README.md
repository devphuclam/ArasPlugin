# Idea CAD Connector — Project Structure

```
IdeaCadConnector/
├── README.md
├── IdeaCadConnector.sln
├── lib/                                    # External dependencies (IOM.dll)
├── src/
│   ├── IdeaCadConnector.Core/              # Shared contracts & DTOs
│   │   ├── Cad/
│   │   │   ├── CadConstants.cs             # Inventor authoring tool, classification, extension
│   │   │   └── CadOpenMode.cs              # Edit / ReadOnly enum
│   │   ├── Contracts/
│   │   │   ├── IArasCadClient.cs           # Interface: 8 async methods
│   │   │   └── ICadApplicationAdapter.cs   # CAD application abstraction
│   │   ├── Dto/                            # 19 request/result DTOs
│   │   ├── Errors/
│   │   │   ├── ArasErrorCode.cs            # 13 typed error codes
│   │   │   └── ArasOperationException.cs   # Exception with ErrorCode + Retryable + Details
│   │   └── Validation/
│   │       └── CadFileNamingRules.cs
│   │
│   ├── IdeaCadConnector.Aras/              # Hybrid IOM + OData client
│   │   ├── ArasClientOptions.cs            # Server URL, database, timeout
│   │   ├── ArasAuthenticator.cs            # IOM PasswordTokenProvider + OData token
│   │   ├── PartSearchClient.cs             # OData $filter / $expand Part search
│   │   ├── ArasCadClient.cs                # Full IArasCadClient implementation
│   │   ├── ServerMethods/                  # C# methods deployed to Aras server
│   │   │   ├── idea_EnsurePrimaryInventorPartCad.cs
│   │   │   └── idea_CommitCadCheckin.cs
│   │   └── IdeaCadConnector.Aras.csproj
│   │
│   ├── IdeaCadConnector.Workspace/
│   │   ├── WorkspaceService.cs             # Local file layout
│   │   ├── WorkspaceOptions.cs
│   │   └── IdeaCadConnector.Workspace.csproj
│   │
│   ├── IdeaCadConnector.Ui/
│   │   ├── Views/
│   │   │   ├── LoginDialog.xaml / .cs      # WPF login + search UI
│   │   │   └── ...
│   │   ├── ViewModels/
│   │   │   └── LoginViewModel.cs
│   │   └── IdeaCadConnector.Ui.csproj
│   │
│   └── IdeaCadConnector.Inventor/          # Inventor ApplicationAddInServer
│       ├── StandardAddInServer.cs          # 6 ribbon buttons, button handlers
│       ├── InventorCadAdapter.cs           # Read/write Inventor property sets
│       ├── IdeaCadConnector.Inventor.addin # Inventor add-in registration
│       └── IdeaCadConnector.Inventor.csproj
```

## Architecture: Hybrid IOM + OData

```
┌───────────────────────────────────────────────────────────┐
│                    StandardAddInServer                    │
│              (Inventor ribbon buttons)                    │
└──────────┬────────────────────────────────────┬───────────┘
           │ uses                               │ uses
           ▼                                    ▼
┌──────────────────────┐          ┌──────────────────────────┐
│   ArasCadClient      │          │  InventorCadAdapter      │
│  (IArasCadClient)    │          │  (ICadApplicationAdapter)│
│                      │          └──────────────────────────┘
│  ┌────────────────┐  │
│  │ArasAuthenticator│ │  IOM.dll → Innovator instance
│  │                │ │  PasswordTokenProvider → Login()
│  └────────────────┘  │
│  ┌────────────────┐  │
│  │PartSearchClient │  │  OData → server/odata/Part
│  └────────────────┘  │
│                      │
│  IOM operations:     │
│  • inn.applyMethod() │  → idea_EnsurePrimaryInventorPartCad
│  • inn.applyMethod() │  → idea_CommitCadCheckin
│  • inn.newItem()     │  → lock / unlock / get CAD
│  • attachPhysicalFile│  → file upload
│  • fetchFileProperty │  → file download
└──────────────────────┘
```

### ArasCadClient Method → IOM Mapping

| Interface Method | IOM Implementation |
|---|---|
| `LoginAsync` | `PasswordTokenProvider` + `IomFactory.CreateHttpServerConnection()` + `connection.Login()` |
| `SearchPartsAsync` | Delegated to `PartSearchClient` (OData HTTP GET) |
| `CreateInventorPartCadAsync` | `inn.applyMethod("idea_EnsurePrimaryInventorPartCad", aml)` |
| `CheckoutAsync` | `inn.newItem("CAD", "lock")` → `inn.newItem("CAD", "get")` |
| `OpenReadOnlyAsync` | `inn.newItem("CAD", "get")` (no lock) |
| `UploadFileAsync` | `inn.newItem("File", "add")` → `attachPhysicalFile(path)` |
| `CheckinAsync` | `inn.applyMethod("idea_CommitCadCheckin", aml)` |
| `CancelCheckoutAsync` | `inn.newItem("CAD", "unlock")` |
| `DownloadNativeFileAsync` | `inn.newItem("File", "get")` → `fetchFileProperty("file_body", path)` |

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

`IOM.dll` is **not on NuGet**. It ships with Aras Innovator (found under the server's
`Client/` folder or the Innovator Client installation). The `.csproj` expects it at:

```
src/IdeaCadConnector.Aras/lib/IOM.dll
```

If your Aras installation is at `C:\Program Files\Aras\Innovator\`, copy from:
```
C:\Program Files\Aras\Innovator\Client\IOM.dll
→ src/IdeaCadConnector.Aras/lib/IOM.dll
```

## Design Decisions

1. **Why IOM instead of REST?** IOM is the official Aras .NET SDK. It handles SOAP
   envelope construction, session management, token refresh, and error parsing.
   The previous REST implementation hand-built all of this, resulting in fragile code
   with no testability benefit.

2. **Why keep OData for search?** AML's search/filter capabilities are more limited than
   OData `$filter`/`$expand`. The Part search with OData is a single HTTP GET; keeping it
   over REST avoids AML complexity and makes response parsing trivial with `System.Text.Json`.

3. **Why not full IOM?** The `Part` OData endpoint has `$expand=Part_CAD` which returns
   the linked CAD items in one round trip. AML would require multiple `apply()` calls,
   making search slower and more complex.

4. **No refresh-token logic.** The hybrid client does not implement OAuth refresh-token
   grants. If the IOM session or OData token expires, the user re-logs in. This simplifies
   the code and avoids edge cases where refresh succeeds but the IOM session has expired.
