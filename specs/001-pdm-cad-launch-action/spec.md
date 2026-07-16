# PDM CAD Launch Action

## Overview

Provide one persistent primary CAD action for a selected actionable PDM node so users can distinguish checkout-and-open, reopening an owned checkout, and read-only opening.

## User-visible requirements

### FR-001 Persistent actionable selection

For a selected non-root node with a primary CAD reference, show one primary CAD action. Keep it visible when prerequisites are incomplete, but disable it and show an actionable reason.

### FR-002 Action modes

Expose these user-visible modes:

- Checkout and open when the CAD is editable, connected, available, and has no active local checkout.
- Open in IronCAD when a valid local checkout is owned by the current session.
- Open read-only when the CAD is released/non-editable or locked by another user and a native/local file is available.
- Unavailable when connection, live identity, lifecycle state, or file prerequisites are incomplete.

### FR-003 Structural visibility

Do not show the primary CAD action for root assembly rows or rows without a primary CAD reference.

### FR-004 Consistent presentation state

Visibility, enablement, label, localized disabled reason, and tooltip are projections of one presentation state and refresh when selection, connection, live CAD state, lock ownership, file state, local manifest, or busy state changes.

### FR-005 Existing workflow preservation

Use the existing Open-in-IronCAD command and preserve existing checkout, check-in, cancel-checkout, lock, download, and read-only behavior.

### FR-006 Accessibility and localization

The action remains keyboard accessible, exposes a tooltip when disabled, and provides localized labels/reasons in English, Vietnamese, and Japanese.

## Constraints

- .NET Framework `net48`, existing WPF/MVVM structure, existing localization resources, and xUnit tests.
- No second checkout command.
- No Aras schema change or invented remote contract.
- No product behavior outside this feature's approved scope.

## Acceptance criteria

- AC-001 Every action mode and structural visibility case has a deterministic test.
- AC-002 Unavailable actionable rows remain visible, disabled, and explain why.
- AC-003 Existing command binding remains the only checkout/open command.
- AC-004 State refresh raises all dependent presentation properties.
- AC-005 Localized labels and disabled reasons exist in English, Vietnamese, and Japanese.
- AC-006 Focused tests, full tests, and Desktop build pass.

## Out of scope

New Aras schema, new remote checkout protocol, new command, CAD file format changes, product synchronization redesign, and manual IronCAD UAT execution not represented by repository evidence.

## Legacy traceability

- Design: `docs/archive/legacy-ai-work-kit/docs/superpowers/specs/2026-07-15-pdm-cad-launch-action-design.md`.
- Technical plan: `docs/archive/legacy-ai-work-kit/docs/superpowers/plans/2026-07-15-pdm-cad-launch-action.md`.
- Historical implementation commits: `18de2f5` and `07cf495`.
