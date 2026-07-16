# PDM CAD Launch Action UX Design

## Problem

The PDM Projects view merged Checkout into a generic “Open in IronCAD” command and hides the button whenever live state is incomplete or temporarily not actionable. Users cannot tell whether clicking opens read-only, reopens a checkout, or creates a checkout, and the primary action appears to disappear.

## UX Contract

For any selected non-root node with a primary CAD reference, show one persistent primary CAD action. Its mode is derived from live CAD and local checkout state:

1. **Checkout & Open IronCAD** — editable CAD, no active local checkout, not locked by another user.
2. **Open in IronCAD** — a valid local checkout owned by the current session.
3. **Open Read-Only** — Released/non-editable CAD or CAD locked by another user when a native/local file is available.
4. **Unavailable** — CAD selection exists but connection, live ID, lifecycle state, or file prerequisites are incomplete. Keep the button visible but disabled and show an actionable reason in its tooltip.

Root assembly rows and rows without a primary CAD do not show the action because they do not identify an actionable CAD document.

## Architecture

Add a small presentation model (`PdmCadLaunchActionState`) computed by `PdmProjectsViewModel`. It contains mode, localized label key, availability, and disabled reason. `HasOpenInIronCadAction`, `CanOpenInIronCad`, `OpenInIronCadModeText`, and the tooltip are projections of this one state so visibility, command enablement, and text cannot disagree.

Keep the existing `OpenInIronCadCommand` and its server workflow. Do not create a second checkout command or change Aras lock/download/check-in/cancel behavior. Refresh the presentation state whenever selection, connection, live CAD state, lock ownership, native-file state, local manifest, or busy state changes.

## Localization and Accessibility

Add localized keys for Checkout & Open, checked-out Open, read-only Open, and disabled reasons in English, Vietnamese, and Japanese. The button remains keyboard accessible through the existing command binding. Disabled state must have a tooltip and must not rely on color alone.

## Verification

View-model tests cover every mode, persistent visibility during unavailable live state, command enablement, root/no-CAD hiding, state refresh notifications, and localized labels. Existing checkout/check-in tests, the full suite, and Desktop build must pass. A manual smoke test selects `DEMO-A01`, verifies “Checkout & Open IronCAD,” performs checkout, and verifies the label becomes “Open in IronCAD” while Check-in and Cancel Checkout appear.
