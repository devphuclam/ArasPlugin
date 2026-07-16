# Dependency Boundaries

## Project responsibilities

- `IdeaCadConnector.Core`: contracts, DTOs, validation, lifecycle and workflow policies.
- `IdeaCadConnector.Workspace`: local workspace state, naming, package import/export, normalization, clone preparation, and push-preview models.
- `IdeaCadConnector.Aras`: authentication, remote repository clients, search, file transfer, and server-method calls.
- `IdeaCadConnector.Ui`: shared WPF views and view models.
- `IdeaCadConnector.Desktop`: application composition, UI orchestration, session services, and CAD launch actions.
- `IdeaCadConnector.IronCAD`: IronCAD add-in and CAD adapter behavior.
- `IdeaCadConnector.Tests`: verification of observable behavior and contracts.

## Non-negotiable boundaries

- No dependency cycles.
- Aras- or IronCAD-specific behavior stays behind suitable abstractions when consumed by Core.
- Local persistence is not implemented in remote integration projects.
- UI projects do not own reusable domain algorithms or raw remote protocol details.
- A library entry references an existing Part and does not duplicate Part/CAD/File records.

## Hard gates

Schema-dependent work requires current verified schema evidence. Document file-link behavior, remote pull, branch-head semantics, and promotion/conflict behavior require their corresponding contracts and evidence before implementation.

Source references: `docs/ai/roadmap/DEPENDENCY_MAP.md`, `docs/ai/03_ARCHITECTURE_RULES.md`, project files under `src/`.
