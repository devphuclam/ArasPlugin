# Environment Configuration

## Config File Name

```text
IdeaCadConnector.environment.json
```

## Lookup Order

The config file is resolved in the following order:

1. **Environment variable**: `IDEA_CAD_CONNECTOR_ENV_CONFIG` — if set and the file exists at that path.
2. **Next to executable**: `app/IdeaCadConnector.environment.json` in the same folder as the desktop executable.
3. **User profile**: `%APPDATA%/IdeaCadConnector/IdeaCadConnector.environment.json`.
4. **Built-in defaults**: If no file is found, the app uses safe built-in defaults. No crash.

## Supported Fields

| Section | Field | Type | Default | Notes |
|---------|-------|------|---------|-------|
| (root) | `schemaVersion` | int | `1` | Must be `1`. Rejects unknown versions. |
| (root) | `environmentName` | string | `"Default"` | Informational label. |
| `aras` | `baseUrl` | string | `""` | Aras Innovator server base URL. Used for API calls and login prefill. |
| `aras` | `database` | string | `""` | Aras database name. Used for login prefill. |
| `aras` | `openInArasBaseUrl` | string | `""` | Separate URL for Open-in-Aras links if different from API base. |
| `local` | `vaultCacheDirectory` | string | `%LOCALAPPDATA%/IdeaCadConnector/VaultCache` | Vault cache root. Supports %LOCALAPPDATA%, %APPDATA%, %USERPROFILE%. |
| `local` | `ironCadExecutablePath` | string | `""` | Override for IronCAD executable path. Empty = use default or file association. |
| `local` | `openDownloadedCadAfterDownload` | bool | `false` | Whether to auto-open CAD after downloading. |
| `roles` | `managerUsers` | string[] | `["TPTKC"]` | User aliases with manager capability. |
| `roles` | `reviewerUsers` | string[] | `["TNTKC", "TPTKC"]` | User aliases with reviewer capability. |
| `roles` | `contributorUsers` | string[] | `["NVTKC", "TNTKC", "TPTKC"]` | User aliases with contributor capability. |
| `roles` | `readOnlyUsers` | string[] | `["NVLCR", "PM", "KhachHang", "Customer"]` | User aliases with read-only access. |
| `diagnostics` | `logLevel` | string | `"Info"` | Log level. Not yet wired. |
| `diagnostics` | `enableFileLogging` | bool | `false` | Whether to log to a file. Not yet wired. |
| `diagnostics` | `logDirectory` | string | `%LOCALAPPDATA%/IdeaCadConnector/Logs` | Log file directory. Not yet wired. |

## Secret Policy

The config file is for **non-secret environment settings only**.

The following keys are **rejected with a warning** if found anywhere in the config file:

- `password`
- `token`
- `secret`
- `cookie`
- `session`
- `credential`
- `passphrase`
- `auth`
- `apikey`
- `api_key`

If any of these keys appear, a warning is emitted during config load. The config is still accepted but the user is advised to remove the secret.

**Do not put passwords, tokens, or credentials in this file.**

For credentials, use the standard Aras login dialog at app startup.

## Fallback Behavior

- **Missing file**: The app uses built-in defaults with a warning. No crash.
- **Empty file**: Same as missing — defaults used.
- **Malformed JSON**: A clear parsing error is shown. Defaults are used as fallback.
- **Unsupported schemaVersion**: An error is shown. Defaults are used.
- **Secret-like keys detected**: Warning is emitted. Config is still loaded.

## Role Mapping

The `roles` section in the config overrides the built-in role defaults. The official role identities are:

| ID | Official Title | Default Capability |
|---|---|---|
| `TPTKC` | Trưởng phòng thiết kế cơ | Manager |
| `TNTKC` | Trưởng nhóm thiết kế cơ | Reviewer |
| `NVTKC` | Nhân viên thiết kế cơ | Contributor |
| `NVLCR` | Nhân viên lắp ráp cơ | Read-only |
| `PM` | Quản lý dự án | Read-only |
| `Khách hàng` | Customer | Read-only |

If no config file is present, the built-in defaults above are used.

## How to Set Up

1. Copy the template file:
   ```
   IdeaCadConnector.environment.template.json
   ```
   to:
   ```
   IdeaCadConnector.environment.json
   ```

2. Place the file in one of the lookup locations (see Lookup Order above).

3. Edit the non-secret values for your environment:
   - Set `aras.baseUrl` to your Aras Innovator server URL.
   - Set `aras.database` to your target database.
   - Optionally set `local.ironCadExecutablePath` if IronCAD is in a non-default location.
   - Optionally adjust `local.vaultCacheDirectory` if you want a custom cache path.

4. Do **not** add passwords, tokens, or secrets to this file.

## Setting the Environment Variable

To force a specific config path, set the environment variable:

```powershell
$env:IDEA_CAD_CONNECTOR_ENV_CONFIG = "D:\Configs\IdeaCadConnector.environment.json"
```

This is useful for CI/CD, test automation, or shared machine setups.

## Current Wiring Status (Sprint 3.2)

| Setting | Wired? | Notes |
|---------|--------|-------|
| `aras.baseUrl` | Not wired yet | Can prefill login dialog. Candidate for Sprint 3.3. |
| `aras.database` | Not wired yet | Can prefill login dialog. Candidate for Sprint 3.3. |
| `aras.openInArasBaseUrl` | Not wired yet | Candidate for Sprint 3.3. |
| `local.vaultCacheDirectory` | Not wired yet | PartLibraryVaultService has its own default. Candidate for Sprint 3.3. |
| `local.ironCadExecutablePath` | Not wired yet | AppSessionContext has IronCadExecutablePath. Candidate for Sprint 3.3. |
| `local.openDownloadedCadAfterDownload` | Not wired yet | Future candidate. |
| `roles.*` | Documented only | Config model supports it. Actual wiring to LibraryAuthorizationRules is Sprint 3.3+ candidate. |
| `diagnostics.*` | Not wired | Requires logging infrastructure. Future candidate. |

## Per-Machine Setup

Each user/machine that runs the app should:

1. Have their own `IdeaCadConnector.environment.json`.
2. Set their own Aras server URL and database.
3. Set their own vault/cache path if the default is not suitable.
4. Leave role mapping as-is unless the organization has different user aliases.
5. Never share config files containing environment-specific values across machines unless they are identical environments.
