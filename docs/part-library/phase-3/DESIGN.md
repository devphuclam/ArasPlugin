# Phase 3 Design

## Scope

Phase 3 makes the desktop connector deployable as an internal release candidate. Sprint 3.1 intentionally avoids runtime feature changes and focuses on a repeatable release package process.

## Release Package Model

The release zip contains one root folder:

```text
IdeaCadConnector-v0.3.0-rc1/
  app/
  aras/
    server-methods/
    README-Aras-Deployment.md
  docs/
  checksums/
  VERSION.txt
```

The `app/` folder is copied from the desktop Release output folder. This keeps runtime DLL selection aligned with MSBuild and avoids guessing transitive dependencies.

## Configuration Model

The current desktop app has no `App.config`. The only desktop project file explicitly copied to output is:

```text
pdm-naming-policy.json
```

Aras connection details are runtime/session-driven. Sprint 3.1 documents required environment values but does not refactor startup configuration. Externalized connection profiles are deferred to Sprint 3.2.

## Aras Dependency

The only required Aras method for this release candidate is:

```text
idea_GetPrimaryIronCadForPart
```

It is read-only and returns the best primary IronCAD CAD linked to a Part through `Part CAD`. The package includes the method source as deployment reference material.

## Packaging Script Design

`tools/release/package-release.ps1`:

- validates repository paths;
- builds the solution in the requested configuration;
- stages app output, Aras method source, and docs;
- writes `VERSION.txt`;
- writes `checksums/SHA256SUMS.txt`;
- creates `IdeaCadConnector-<Version>.zip`;
- never copies source `bin/obj`, `.vs`, test results, Vault/cache folders, or credentials.

## Risks

- The package is a zip, not an installer; users need manual extraction guidance.
- Environment configuration is still partly manual.
- IronCAD open depends on a local IronCAD installation and path association.
- Vault download depends on live Aras File/Vault permissions.
