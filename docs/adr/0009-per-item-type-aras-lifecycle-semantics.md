# ADR-0009: Per-ItemType Aras Lifecycle Semantics

## Status
Accepted

## Context

The verified Aras environment contains different lifecycle maps for Part, CAD, Document, and Project. Some display names overlap, but their transitions and business meanings do not necessarily match. Treating lifecycle names as one shared enum would make CAD-to-Part and BOM synchronization unsafe and would force the PDM user experience into a Git-like model.

## Decision

Keep lifecycle identity scoped to the Aras ItemType and lifecycle map. The application may expose a semantic role and capabilities for policy decisions, but it must retain the verified Aras state identity and display name. CAD-to-Part and BOM parent promotion must use explicit, verified mapping and eligibility policies. Local change status, checkout ownership, validation, lifecycle state, and ChangeSet outcome remain separate dimensions; ChangeSet is an internal audit/synchronization record, not a Git commit workflow for users.

## Consequences

Lifecycle policies become explicit and testable, and the UI can remain familiar to CAD engineers. The next implementation step is to add a resolver/policy boundary for lifecycle semantics before redesigning Push as a PDM Check-in operation.
