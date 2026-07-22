# IDEA PDM Product Discovery Brief

**Status**: Approved discovery input for Spec Kit
**Date**: 2026-07-18
**Audience**: IDEA Technology product owner, analysts, Spec Kit writers, implementers, and reviewers

## Purpose

This brief records the agreed product model and scope discovered before formal feature specification. It is an input to `/speckit.specify`; it is not a competing `spec.md`, implementation plan, or task list.

## Product Direction

IDEA Technology is building a PDM application for design engineers who work with IronCAD and Autodesk Inventor. The current application uses Aras Innovator both as a working backend and as a reference for established PDM behavior. The target is an IDEA-owned PDM system whose business model is not permanently coupled to Aras.

The product follows PDM authority rules for identity, revision, lifecycle, permissions, review, and release. It borrows selected Git-like mechanisms only for local workspace safety, change detection, history, and recovery.

## Product Outcomes

The product must enable:

1. Engineers to obtain controlled CAD content, edit it locally, understand what changed, and check it in safely.
2. Engineers and reviewers to move a design through a controlled review process to `Released`.
3. Released designs to remain immutable and auditable.
4. Subsequent design changes to create a new revision rather than alter a released revision.
5. Product structures and BOMs to be revision-controlled without unsafe automatic merging.
6. The current Aras authority to be replaceable by a future IDEA authority without redefining the user-facing PDM concepts.

## Actors

- **Design Engineer**: creates and edits Part, CAD, Document, and BOM content; checks content out and in; submits designs for review.
- **Reviewer**: inspects a submitted design and either approves it or requests rework.
- **Project Manager**: observes design status, product structure, ownership, and release progress; does not bypass engineering controls by default.
- **PDM Administrator**: manages users, roles, permissions, numbering policies, lifecycle mappings, and environment configuration.

## MVP Scope

The MVP covers controlled engineering work through `Released`:

- IronCAD and Autodesk Inventor design workflows.
- Part, CAD, Document, and BOM identities and relationships.
- Local Workspace creation and synchronization from an explicit baseline.
- Checkout, read-only open, check-in, local change detection, and ChangeSet history.
- Review submission, approval, and request-rework behavior.
- Release and post-release revision creation.
- Role-aware actions for engineers, reviewers, project managers, and PDM administrators.
- Aras Innovator as the current authority through an adapter.
- Backend-neutral domain meaning so an IDEA-owned authority can be introduced later.

## Explicitly Out of Scope

- Manufacturing execution and shop-floor control.
- Procurement, receiving, inventory, and warehouse status.
- `Che tao` and `Nhan hang` lifecycle states.
- General-purpose Git source control exposed as the engineer's PDM model.
- Automatic merge of binary CAD, drawing, PDF, or other engineering files.
- Uncontrolled modification or history rewriting of released data.
- Full ECO/ECN/CMII change management in the first MVP unless separately specified.
- A decision to permanently replace Aras or permanently synchronize with it.

## Domain Model

### PDM Authority

The PDM Authority is the system of record for remote identity, lifecycle, permissions, released revisions, and audit history. Aras fills this role now. A future IDEA backend may fill it later.

### Part and Revision

A **Part** is a stable engineering identity, normally represented by a Part Number. A **Part Revision** is a controlled version of that identity such as revision A or B.

- A Part identity may have many revisions.
- At most one revision is the current working or current released revision under the applicable policy.
- A released Part Revision is immutable.

### CAD and Document

A **CAD record** controls native IronCAD or Inventor design content and is normally linked to a Part Revision. A **Document** controls related non-native-CAD engineering content.

CAD, Part, and Document lifecycle identities are scoped to their own configured types. Matching state labels do not prove identical behavior. Cross-item promotion requires explicit policy.

### BOM

A **BOM** is the revision-controlled parent-child product structure associated with a Part Revision. Each BOM line identifies a child Part/configuration, quantity, and ordering information required by the approved feature.

A BOM belonging to a released parent revision is immutable. Changes require a new working revision of the affected parent configuration.

### Workspace and Baseline

A **Workspace** is an engineer's local controlled working copy. Its **Baseline** records the remote configuration from which the workspace was cloned or last synchronized.

Local change status is calculated against the Baseline and is independent from lifecycle, checkout ownership, validation, and synchronization outcome.

### ChangeSet

A **ChangeSet** is an immutable record of one intended or completed synchronization operation. It includes its Baseline, selected changes, validation result, author, reason, and outcome.

A local file save, a ChangeSet/check-in, and a released revision are different history events.

## Core Relationships

```text
Part identity
  -> Part revision
       -> CAD revision(s)
       -> Document revision(s)
       -> BOM snapshot

Workspace
  -> Baseline remote configuration
  -> Local files and metadata
  -> Local change statuses
  -> ChangeSet history
```

## Lifecycle and Revision Rules

The approved MVP lifecycle is:

```text
Khoi tao
  -> Thiet ke chi tiet
  -> In Review
       -> Request Rework -> Thiet ke chi tiet
       -> Approve -> Released
```

After release:

```text
Released revision A
  -> Start New Revision
  -> working revision B at Khoi tao
  -> Thiet ke chi tiet
  -> In Review
  -> Released revision B
```

The previous revision remains immutable and auditable. It may later receive a semantic role such as `Superseded` under an approved policy, but the MVP must not destroy or rewrite it.

## Domain Invariants

1. The PDM Authority is authoritative for remote identity, permissions, lifecycle, and released revisions.
2. A released revision cannot be edited in place.
3. A design change after release creates a new working revision.
4. Local save history, ChangeSet/check-in history, and released revision history remain separate.
5. Local change status, checkout status, lifecycle state, validation status, and synchronization outcome remain separate dimensions.
6. A Workspace always compares changes against an explicit Baseline.
7. Binary engineering content is never automatically merged.
8. A live push/check-in must validate identity, permissions, lifecycle eligibility, checkout ownership, Baseline freshness, and content safety.
9. Partial remote or local failure must not leave an apparently successful mixed configuration.
10. Part, CAD, Document, and BOM transitions are coordinated through explicit verified policy, not matching state-name strings.
11. Git branch names are not the primary product or revision model presented to design engineers.
12. Credentials and environment secrets are never PDM domain data or audit content.

## Git-Inspired Workspace Behavior

The product may adopt these mechanisms:

- Content hashing for change detection and integrity.
- Diff views for Added, Modified, Deleted, and Unchanged content.
- Immutable ChangeSet records.
- Clone, pull, preview, and push/check-in concepts adapted to controlled PDM semantics.
- Local snapshots, recovery, and optional internal staging/sandbox isolation.

The product must not adopt these Git semantics as PDM rules:

- Branches as official product revisions.
- Force-push or released-history rewriting.
- Automatic merge of binary CAD or released BOM configurations.
- Commit identity as a substitute for Part identity, revision, lifecycle, or approval.
- A Git tag as a substitute for a controlled release decision.

## Backend-Neutrality Constraint

New product requirements must be expressed through PDM concepts and business actions, not Aras transport names. The current Aras adapter maps those concepts to verified ItemTypes, lifecycle states, permissions, Vault operations, and server methods.

No requirement may assume a future IDEA backend has Aras AML, IOM, Vault, ItemType names, or Aras identifiers. Current Aras-specific behavior remains valid adapter evidence until a replacement authority is approved.

## Recommended Feature Decomposition

Run one `/speckit.specify` invocation per feature:

1. **Controlled CAD design release**: checkout/check-in, review/rework, release, and new revision for Part-linked CAD.
2. **Local Workspace and ChangeSet synchronization**: Baseline, scan/diff, preview, safe check-in/pull, audit, and recovery.
3. **Revision-controlled product structure**: BOM editing, validation, released snapshot, where-used, and revision impact.
4. **Controlled engineering Documents**: physical file versioning, relationships, release, and retrieval.
5. **PDM administration and role policy**: users, permissions, numbering, lifecycle mapping, and environment configuration.

Manufacturing, procurement, receiving, and full formal change management are later product areas.

## First Spec Kit Invocation

Use the following as the first bounded feature description:

```text
/speckit.specify Create the IDEA PDM controlled CAD design release workflow using docs/product/idea-pdm-product-discovery.md as approved discovery context. A design engineer working in IronCAD or Autodesk Inventor must be able to obtain a Part-linked CAD working revision, check it out for editing, check it in with an auditable reason, submit it for review, and receive a request for rework. An assigned reviewer must be able to approve an eligible design and move it to Released. A Released revision must be read-only; starting further design work must create a new working revision while preserving the released revision. Project managers can observe progress, and PDM administrators configure permissions and lifecycle mappings without bypassing release history. Aras Innovator is the current authority, but requirements must use backend-neutral PDM language and must not expose Git branches as the engineer's revision model. Exclude BOM editing, manufacturing, procurement, receiving, automatic binary merge, and full ECO/ECN processing from this feature.
```

After the spec is created, run `/speckit.clarify` if it contains unresolved business decisions. Do not proceed to planning until acceptance scenarios and permission rules are explicit.

## Evidence and Constraints

- Current source and tests remain authoritative for implemented behavior.
- Live Aras schema, lifecycle, permission, Vault, and server-method behavior require verified environment evidence.
- The repository constitution and architecture boundaries remain mandatory.
- This brief does not authorize production schema changes, registry changes, or secret handling.
