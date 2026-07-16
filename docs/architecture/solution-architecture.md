# Solution Architecture

## System shape

The solution targets .NET Framework `net48` and contains shared contracts, local workspace services, Aras integration, shared UI, desktop orchestration, IronCAD integration, and tests. The current solution/project references are authoritative; this document summarizes stable ownership only.

## Layer ownership

| Area | Responsibility | Boundary |
|---|---|---|
| Core | Contracts, DTOs, policies, validation, shared domain abstractions | No WPF, filesystem implementation, or transport implementation |
| Workspace | Local manifests, scan/diff, package import/export, normalization, clone preparation, atomic local file operations | No Aras transport or WPF dialogs |
| Aras | Remote clients, authentication, item operations, Vault transfer, server-method integration | No WPF UI or local workspace persistence |
| Ui | Shared WPF views and view models | No Aras/business persistence |
| Desktop | Application orchestration, ViewModels, navigation, services | No raw AML or reusable diff algorithms |
| IronCAD | IronCAD adapter, add-in, and CAD-specific integration | No Aras repository orchestration |
| Tests | Unit and integration verification | No production-only shortcuts |

## Current flows

The desktop application composes Core contracts with Workspace, Aras, Ui, and IronCAD services. Aras clients and adapters isolate remote operations; Workspace owns local package and manifest behavior; CAD adapters isolate application-specific behavior.

## Safety rules

- Keep WPF dependencies out of Core and Workspace.
- Keep raw Aras calls in the Aras project or server-method sources.
- Keep `.idea-pdm` persistence in Workspace.
- ViewModels orchestrate services rather than implementing hashing, diff, or Vault protocols.
- Binary CAD/PDF/DWG content is not auto-merged.
- Upload, pull, manifest, and branch-head updates follow staged/atomic rules defined by the relevant contracts.

## Compatibility

Changes to manifest, schema, or public contracts require a version field, compatibility handling, tests for old and new shapes, and rollback notes.

Source references: `IdeaCadConnector.sln`, `Directory.Build.props`, and archived source `docs/archive/legacy-ai-work-kit/docs/ai/03_ARCHITECTURE_RULES.md`.
