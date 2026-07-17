# ArasPlugin Domain Context

## Product purpose

ArasPlugin, whose current solution is named `IdeaCadConnector`, connects local CAD work with Aras Innovator PDM workflows. The repository contains desktop UI, local workspace/package handling, Aras communication, and IronCAD integration. Exact product scope beyond the behavior represented by the current source and tests is not yet established.

## Core domain entities

- **Part**: an Aras product/item record searched, created, reused, and related to CAD, documents, BOM, or library records by the current workflows.
- **CAD**: an Aras CAD record associated with a native CAD file and a Part; checkout, check-in, revision, and read-only opening are represented by current contracts.
- **Document**: an Aras document record used by current PDM and library workflows. Physical file/version policy beyond the verified schema map is not yet established.
- **Workspace**: the local representation of project files, manifests, hashes, references, and package operations.
- **Local file/package representation**: files such as IronCAD `.ics` content and generated PDM package/manifest data. Package publication, import, validation, and external-reference rules are represented by the Workspace code.

## External systems

- **Aras Innovator**: accessed through verified repository clients and adapters, including Aras IOM where used by the current implementation. Authentication, remote item operations, Part search, file transfer, and library/PDM operations are separated behind contracts and adapters.
- **IronCAD**: the CAD application integrated through the IronCAD adapter and desktop services. The repository resolves and launches an `IRONCAD.exe` executable when the current workflow requires it; exact installation behavior is defined by code and tests.
- **Windows/.NET Framework**: the supported execution environment; the solution targets `.NET Framework net48` and includes WPF/WinForms, COM, and strong-name constraints.

## Project dependency responsibilities

- Core defines contracts, DTOs, validation, lifecycle policies, and other shared domain abstractions.
- Workspace owns local workspace state, naming, package import/export, normalization, clone preparation, and push-preview models.
- Aras implements remote communication, authentication, search, Vault transfer, server-method calls, and repository clients behind Core contracts.
- Ui contains shared WPF views and view models; Desktop composes application workflows and services.
- IronCAD provides the CAD-specific adapter and add-in behavior used by the application.
- A Part may relate to CAD and Document records in Aras; the verified schema map also records Part BOM, Part CAD, Part Document, Project Document, and Part Library relationships.
- A workspace/package may contain local CAD files, metadata, manifests, and references; the exact remote synchronization semantics are defined by the approved feature artifacts for each change.

## Terminology

- **Checkout/check-in**: the current CAD workflow for obtaining an editable file and returning it with a remote update.
- **Clone**: a Workspace operation that builds or consumes a local package from remote PDM data; exact end-to-end sync semantics are not yet established.
- **Normalize/export**: Workspace operations that validate references and publish a package representation with a manifest.
- **Linked normalized export**: A normalized package whose root IronCAD scene keeps each child occurrence linked to its corresponding child CAD file; saving changes made to a child through the root scene must persist those changes to the child file, including when the same child definition is used by multiple occurrences.
- **Part library**: Aras records and client workflows for reusable parts, entries, revision details, and usage.
- **Vault**: Aras-managed physical file storage accessed through the current file client abstractions.

## Domain invariants

- A local package publication must pass the current output-safety, manifest, and reference validation rules.
- Native CAD file handling must respect the current CAD adapter and file validation contracts.
- Aras schema names and remote lifecycle/permission behavior are facts only when supported by verified evidence.
- Credential values, tokens, passwords, and environment-specific secrets are never domain data for documentation.

## Known boundaries

- Live Aras schema, permissions, lifecycles, and server-method behavior must be verified against current environment evidence before dependent work.
- Machine-specific IronCAD, COM, Vault, and OCR behavior is not assumed to be portable between environments.
- Remote synchronization semantics are governed by the approved feature specification and verified integration contracts.
- Product behavior not evidenced by source, tests, or verified documentation is `Not yet established`.

## Detailed references

- `README.md`
- `docs/architecture/solution-architecture.md`
- `docs/domain/aras-and-cad-domain.md`
- `docs/development/build-and-test.md`
- `docs/development/known-limitations.md`
- `docs/security/data-safety.md`
