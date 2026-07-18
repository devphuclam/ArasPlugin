# ArasPlugin Domain Context

## Product purpose

ArasPlugin, whose current solution is named `IdeaCadConnector`, is the learning and delivery base for an IDEA Technology PDM product for design engineers. Aras Innovator is the current PDM authority and reference implementation; the target product keeps business behavior independent enough to support an IDEA-owned authority later.

## Core domain entities

- **Part**: the stable engineering identity of a designed or purchased item across its revisions. A Part may relate to CAD, Documents, a BOM, and library records.
- **Part revision**: one controlled revision of a Part identity. A Released Part revision is immutable.
- **CAD**: a revision-controlled design record associated with native CAD content and normally linked to a Part.
- **CAD revision**: one controlled revision of a CAD identity, including its lifecycle, checkout status, native-content version, and audit history.
- **Document**: a revision-controlled engineering record for non-native-CAD content related to a Part, project, or design configuration.
- **Document revision**: one controlled revision of a Document identity. Document lifecycle and revision propagation are independent policies until explicitly approved.
- **Part-CAD revision pair**: the MVP release aggregate containing one Part revision and one linked CAD revision. The two revisions retain separate lifecycle identities and are coordinated only by explicit operations.
- **BOM snapshot**: the immutable parent-child product structure owned by a specific Part revision. Editing the structure of a Released parent requires a new working Part revision.
- **BOM line**: one parent-child relationship in a BOM snapshot, including the selected child revision policy and quantity.
- **PDM authority**: the system of record for remote identity, lifecycle, permissions, released revisions, and audit history. Aras currently fills this role; a future IDEA backend may fill it without changing the domain meaning.
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
- **Checkout session**: the exclusive editing claim that connects an authority lock, its owner, a local writable copy, and the checkout baseline.
- **Recovery copy**: a verified local copy preserved before destructive workspace cleanup. It is not a new revision or a successful check-in.
- **Clone**: a Workspace operation that builds a local working representation from remote PDM data, including the selected design set and its verified dependencies.
- **ChangeSet**: an immutable record of one intended or completed synchronization operation, including its baseline, selected files, validation result, and outcome. It is an internal PDM audit concept, not the primary user-facing Git workflow.
- **Workspace baseline**: the authoritative remote configuration from which a Workspace was created or last synchronized. Local change status is calculated against this baseline.
- **Working revision**: the editable revision currently progressing through design and review before release.
- **Released revision**: an immutable revision approved for controlled use. Further design changes require a new Working revision.
- **Review submission**: the auditable request to evaluate a working Part-CAD revision pair for release, including the submitter, reviewer, change description, and decision history.
- **Release policy**: the explicit rule that determines which item revisions are eligible for release and which transitions must succeed together.
- **Local change status**: the local file's difference from the workspace baseline: New, Modified, Deleted, or Unchanged. It is independent from Aras lifecycle state and checkout ownership.
- **Lifecycle state**: the state of an Aras item within its own ItemType and lifecycle map. State names are not a shared enum across Part, CAD, Document, and Project.
- **IDEA MVP Part lifecycle**: The initial design workflow is `Khởi tạo` → `Thiết kế chi tiết` → `In Review` → `Released`. A released revision is immutable; later design changes begin through `In Change`, create a new revision, and may mark the previous revision `Superseded`.
- **Lifecycle semantic role**: the business meaning used by the app to reason about a lifecycle state, such as design, review, released, obsolete, or superseded, while retaining the verified Aras state identity and display name.
- **Normalize/export**: Workspace operations that validate references and publish a package representation with a manifest.
- **Linked normalized export**: A normalized package whose root IronCAD scene keeps each child occurrence linked to its corresponding child CAD file; saving changes made to a child through the root scene must persist those changes to the child file, including when the same child definition is used by multiple occurrences.
- **Part library**: Aras records and client workflows for reusable parts, entries, revision details, and usage.
- **Vault**: Aras-managed physical file storage accessed through the current file client abstractions.

## Domain invariants

- A local package publication must pass the current output-safety, manifest, and reference validation rules.
- Native CAD file handling must respect the current CAD adapter and file validation contracts.
- Aras schema names and remote lifecycle/permission behavior are facts only when supported by verified evidence.
- Lifecycle state identity is scoped to the Aras ItemType and lifecycle map; a matching display name does not prove matching business semantics.
- A `Released` Part/CAD revision must not be edited in place; a subsequent change requires a new revision.
- Start New Revision creates a new Part revision and linked CAD revision as one atomic authority operation; the released pair remains unchanged.
- MVP release approval transitions the eligible Part revision and linked CAD revision atomically while preserving their separate lifecycle identities.
- A local file save, a ChangeSet/check-in, and a released revision are distinct history events and must not be treated as equivalents.
- A BOM snapshot belongs to its parent Part revision; a released BOM snapshot is not edited or automatically merged.
- Document revision and lifecycle propagation are not inferred from Part or CAD behavior.
- Modified local content is never silently discarded during cancel-checkout; recovery must succeed before the authority lock is released.
- Local change status, checkout/collaboration status, lifecycle state, validation status, and synchronization outcome are separate dimensions and must not be collapsed into one enum.
- CAD-to-Part and BOM parent promotion requires an explicit verified mapping and eligibility policy; state-name copying is not a valid general rule.
- Credential values, tokens, passwords, and environment-specific secrets are never domain data for documentation.

## Known boundaries

- Live Aras schema, permissions, lifecycles, and server-method behavior must be verified against current environment evidence before dependent work.
- Machine-specific IronCAD, COM, Vault, and OCR behavior is not assumed to be portable between environments.
- Remote synchronization semantics are governed by the approved feature specification and verified integration contracts.
- Aras is authoritative for remote PDM identity, lifecycle, permissions, and released versions; the local Workspace is the editable working copy plus its baseline and ChangeSet history.
- Branches may be used as local or staging implementation details, but they are not the primary PDM user model for CAD engineers.
- Product behavior not evidenced by source, tests, or verified documentation is `Not yet established`.

## Detailed references

- `README.md`
- `docs/architecture/solution-architecture.md`
- `docs/domain/aras-and-cad-domain.md`
- `docs/domain/idea-pdm-domain-model.md`
- `docs/development/build-and-test.md`
- `docs/development/known-limitations.md`
- `docs/security/data-safety.md`
