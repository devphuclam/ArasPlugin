# Workspace and Package Model

## Stable concepts

- A Workspace is the local representation of project files, manifests, hashes, references, and package operations.
- A local package may contain CAD files, metadata, manifests, and external references.
- Workspace services validate package publication, import, normalization, and output safety before applying local changes.
- Clone, import/export, normalization, and push-preview are distinct operations; their exact remote synchronization semantics belong to the approved feature specification and verified integration contracts.

## Domain boundaries

Local file operations must preserve recoverable state and must not overwrite modified workspace content without the approved safety behavior. Binary CAD/PDF/DWG files are not automatically merged.

Detailed implementation references: `src/IdeaCadConnector.Workspace/`, archived `docs/archive/legacy-ai-work-kit/docs/ai/03_ARCHITECTURE_RULES.md`, and canonical `docs/development/known-limitations.md`.
