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

## PDM lifecycle seam

- `IdeaCadConnector.Core` owns the small policy interface that answers business questions such as editable, reviewable, releasable, obsolete, and eligible-for-sync; callers do not compare raw state strings.
- `IdeaCadConnector.Aras` owns the adapter that resolves verified ItemType/lifecycle/state identity and performs remote transitions.
- Mapping between CAD, Part, Document, and Project semantic roles is explicit policy data, not an implicit shared lifecycle enum.
- Workspace change detection and ChangeSet creation remain local concerns; they may block or prepare Check-in, but they do not decide Aras lifecycle transitions.
- Coordinated Part-CAD release and Start New Revision cross the authority seam as atomic business operations. A desktop client must not simulate atomicity by issuing independent transitions and attempting compensating rollback.

## Checkout cancellation seam

- Workspace owns modified-content detection, recovery-copy creation, hash verification, retention metadata, and local cleanup.
- The remote authority adapter owns checkout unlock only; backup paths and local recovery status do not enter its transport request.
- Desktop composes the user confirmation flow and orders recovery verification before remote unlock.

Source references: archived `docs/archive/legacy-ai-work-kit/docs/ai/roadmap/DEPENDENCY_MAP.md`, archived `docs/archive/legacy-ai-work-kit/docs/ai/03_ARCHITECTURE_RULES.md`, and project files under `src/`.
