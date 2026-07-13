# SEC-00-HOTFIX — Configuration Regression Repair Design

## Goal

Repair the merged SEC-00 configuration regressions without changing Aras schema, server methods, Document Vault behavior, or Git history.

## Scope

- Isolate configuration precedence tests from real AppData, application output, and repository-local configuration.
- Make an explicitly set environment-config path authoritative and fail safely when invalid.
- Normalize and validate Aras BaseUri for both file configuration and login overrides.
- Use configured OAuth client and scope in the HTTP authentication path.
- Propagate the configured IronCAD executable path through every PDM adapter construction site.
- Sanitize current tracked documentation, templates, comments, and tests without repeating exposed values.
- Add a correction ticket and project-state record based only on fresh verification evidence.

## Design

`EnvironmentConfigurationLoader` keeps its public `Load()` and `ResolvePath()` behavior while adding an internal path-resolution context for tests. The context supplies the environment value, side-by-side directory, and AppData root. An explicit nonblank environment value is authoritative; missing, directory, unreadable, or malformed files produce controlled errors and never fall back. Tests use one unique temporary root and restore process-global state in `finally` under a non-parallel test collection.

`ArasClientOptions` owns one normalization routine for absolute HTTP/HTTPS BaseUri values. The routine trims trailing separators to one slash and is used by configuration mapping and `WithLoginOverrides`. Authentication endpoints are resolved relative to the normalized BaseUri. OAuth client ID and scope are validated and sent from options by `HttpArasCadClient`.

`PdmProjectsViewModel` uses one narrow adapter-construction helper that reads the existing session options' `IronCadExecutablePath`. All PDM open, read-only, checkout, and edit paths go through it. Missing configuration remains an actionable operation failure; no machine-specific default is introduced.

Sanitization replaces current-tree infrastructure values with documented placeholders or synthetic test values. Historical commits are explicitly left untouched and documented as a separate approved operation.

## Testing strategy

Each defect receives a minimal regression test first and is run alone to establish RED. The smallest production fix is then applied and the same test is run GREEN. Related test groups run after each defect cluster. Full Debug/Release restore, build, and test commands run only after unsafe path tests are isolated. Final verification includes `Check-AiScope`, `Verify-AiTicket`, diff checks, tracked-artifact scans, and current-tree security scans.

## Non-goals

No Aras schema changes, server-method deployment, live Aras connection, DOC-03 work, broad dependency-injection refactor, destructive cleanup, force-push, direct main push, merge, or history rewrite.
