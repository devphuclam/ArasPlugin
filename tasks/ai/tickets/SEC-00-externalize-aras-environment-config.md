# SEC-00 — Externalize Aras environment configuration from source control

## Metadata

- Epic: Security
- Dependencies: None
- Risk: High
- Status: Completed

## Implementation Summary

### Behavior before

`ArasClientOptions.cs` contained hardcoded environment-specific values: an internal Aras server URL (`http://172.16.10.227/InnovatorServer/`), real database name (`InnovatorSolutions`), real Vault ID (`67BBB9204FE84A8981ED8313049BA06C`), and a machine-specific IronCAD executable path. The existing `EnvironmentConfigurationLoader` class was never called from production code — it was dead code only exercised by tests.

### Behavior after

Hardcoded environment-specific defaults are removed from `ArasClientOptions.cs`. All eight config fields now come from a local JSON configuration file loaded at startup via the centralized `ArasClientOptionsFactory`. The search order is:

1. Environment variable `IDEA_CAD_CONNECTOR_ENV_CONFIG`
2. Side-by-side `IdeaCadConnector.environment.json`
3. `%APPDATA%/IdeaCadConnector/IdeaCadConnector.environment.json`
4. Safe built-in defaults (empty/null for infrastructure values, standard safe defaults for OAuth/Timeout/Search)

At login time, user-entered `ServerUrl` and `Database` override the loaded configuration. All other fields (VaultId, Timeout, OAuthClientId, OAuthScope, IronCadExecutablePath, DefaultMaxSearchResults) persist from the loaded configuration.

### Files changed

- EDIT: `src/IdeaCadConnector.Aras/ArasClientOptions.cs`
- EDIT: `src/IdeaCadConnector.Core/Configuration/EnvironmentConfiguration.cs`
- EDIT: `src/IdeaCadConnector.Desktop/App.xaml.cs`
- EDIT: `src/IdeaCadConnector.Desktop/MainViewModel.cs`
- EDIT: `src/IdeaCadConnector.IronCAD/IronCadAddin.cs`
- EDIT: `src/IdeaCadConnector.Ui/Views/LoginDialog.xaml.cs`
- EDIT: `src/IdeaCadConnector.Desktop/IdeaCadConnector.environment.template.json`
- EDIT: `src/IdeaCadConnector.Desktop/IdeaCadConnector.Desktop.csproj`
- EDIT: `.gitignore`
- NEW: `tasks/ai/tickets/SEC-00-externalize-aras-environment-config.md`
- EDIT: `tests/IdeaCadConnector.Tests/EnvironmentConfigurationTests.cs`

### Commands and outputs

- Build Debug: Succeeded (0 warnings, 0 errors)
- Build Release: Succeeded (0 warnings, 0 errors)
- Test Debug: 447 passed, 0 failed, 0 skipped
- Test Release: 447 passed, 0 failed, 0 skipped

### Acceptance criteria mapping

- No real internal Aras server URL in active source/config templates
- No real database name in active source/config templates
- No real Vault ID in active source/config templates
- No machine-specific IronCAD path in hardcoded source
- Local developers configure via `IdeaCadConnector.environment.json` (ignored by Git)
- Missing/invalid required config causes clear actionable error
- 14 new tests cover config loading, validation, and mapping
- Debug and Release builds pass
- All tests pass (447 total)
- No unrelated behavior changes
- No secret or infrastructure value added to logs or documentation

### Schema/manual test impact

- No Aras schema changes
- Manual setup: copy `IdeaCadConnector.environment.template.json` → `IdeaCadConnector.environment.json`, replace placeholders

### Remaining limitations/follow-ups

- Old hardcoded values remain in Git history. History cleanup requires a separate manual security operation.
- Integration tests for IronCAD Add-in and LoginDialog end-to-end config flow were not added (require WPF/COM environment).

## Acceptance criteria

- No real internal Aras server URL remains in active source/config templates.
- No real database name remains in active source/config templates.
- No real Vault ID remains in active source/config templates.
- No machine-specific IronCAD path is required from hardcoded source.
- Local developers can configure the application without editing tracked source files.
- Missing or invalid required configuration causes a clear actionable error.
- Tests cover configuration loading and validation.
- Debug and Release builds pass.
- All tests pass.
- No unrelated behavior changes.
- No secret or infrastructure value is added to logs or documentation.
