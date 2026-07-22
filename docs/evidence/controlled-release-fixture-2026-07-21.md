# Controlled Release Fixture Evidence

**Feature**: `003-controlled-cad-design-release`
**Date**: 2026-07-21
**Environment**: IDEA live Aras Innovator instance
**Scope**: bounded lifecycle path ending at `Released`

## Authorization and safety boundary

The product owner explicitly authorized the disposable fixture pair used for
this test. No bearer token, password, or other credential is recorded here.
No Server Method or ItemType configuration was modified. No CAD file content
was edited or uploaded during this run.

## Fixture

| Item | Number | Revision | Initial state |
|---|---|---|---|
| Part | `DEMO-A05` | A | `Khoi tao` |
| CAD | `DEMO-CAD-A05` | A | `Khoi tao` |

The CAD revision is linked to the Part revision and uses IronCAD as its
authoring tool.

## Observed sequence

| Step | CAD state | Part state | Result |
|---:|---|---|---|
| 0 | `Khoi tao` | `Khoi tao` | Initial read |
| 1 | `Khoi tao` | `Khoi tao` | Opened Edit and used Discard; no content was saved and CAD returned to read-only |
| 2 | `Thiet ke chi tiet` | `Khoi tao` | CAD intermediate promotion succeeded; Part did not follow intermediate CAD state |
| 3 | `In Review` | `Khoi tao` before refresh | CAD review-state promotion succeeded; no error was shown |
| 4 | `Released` | `Released` after Part refresh | CAD release succeeded; CAD `onAfterPromote` invoked `Sync_Part_From_CAD` and the linked Part was released |

The Part state was read again after an explicit Refresh. The final value was
`Released`, so the result is not based on a stale item form.

## Evidence conclusion

This run confirms the deployed coordination path for the bounded release
scenario:

`CAD In Review -> Released` → CAD `onAfterPromote` → `Sync_Part_From_CAD` → linked Part `Released`

It also confirms that the intermediate CAD transition to `Thiet ke chi tiet`
does not release or otherwise advance the Part. The run does **not** prove
failure-path rollback, audit completeness, notification delivery, reviewer
assignment read-contract behavior, or concurrent revision conflict handling.
Those remain separate evidence tasks.

## Reproducibility notes

- The operation was performed through the Aras UI on the named disposable
  fixture pair.
- The exact final state was verified from the CAD results row and refreshed
  Part state field.
- The browser session was handed off after verification.
- The fixture is intentionally left at `Released`; no new revision was
  created in this run.
