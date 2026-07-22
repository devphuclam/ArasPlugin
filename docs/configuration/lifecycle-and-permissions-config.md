# Lifecycle and Permissions Configuration

**Status**: Reference documentation for Aras administrator.
**MVP scope**: Aras-side configuration is performed in the Innovator UI. The client respects Aras permission rejections.

## Required ItemTypes

- CAD Document (or equivalent custom CAD ItemType)
- Part

## Required Lifecycle Maps

### CAD Lifecycle

States (verified): `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, `In Change`, `Superseded`, `Loai bo`

Transitions (expected):
- `Khoi tao` → `Thiet ke chi tiet`
- `Thiet ke chi tiet` → `In Review`
- `In Review` → `Released` (approve)
- `In Review` → `Thiet ke chi tiet` (request rework)
- `In Review` → `Thiet ke chi tiet` (withdraw)
- `Released` → `In Change` (via ECO/change order)
- `In Change` → `Thiet ke chi tiet` (revision complete)

### Part Lifecycle

Target IDEA profile: `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, `In Change`, `Superseded`

Transitions (expected):
- Part follows its own lifecycle mapping per ADR-0009/ADR-0011; it does not reuse CAD raw state constants in Core.
- `In Review` → `Released` (coordinated with CAD at MVP release approval)

**Live environment note (2026-07-20)**: the active Part ItemType is currently assigned to `Custom Part`, whose observed states include `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, `In Change`, `Superseded`, `Obsolete`, `Che tao`, and `Nhan hang`. The core IDEA profile is therefore present in the live map; the administrator still needs to verify the transition graph and role permissions before enabling dependent actions.

## Role-to-Action Permission Setup

The desktop role resolver consumes explicit configured user lists. Add PDM Administrator users under `roles.pdmAdministratorUsers`; PDM Administrators have full client-side role authority for CAD business actions and workspace operations. During development, this explicit administrator role also enables the direct Approve/Request Rework test path when Aras has not assigned a reviewer yet. It does not grant or simulate Aras permissions; Aras remains authoritative and may still reject the operation. Users not present in exactly one configured role list remain `Unknown` and are fail-closed for engineering actions.

Configure in Aras Innovator UI:

| Action | Required Role |
|--------|--------------|
| Checkout | Design Engineer |
| Check-in | Design Engineer |
| Cancel Checkout | Design Engineer |
| Submit for Review | Design Engineer |
| Approve | Reviewer |
| Request Rework | Reviewer |
| Withdraw | Design Engineer (owning engineer) |
| Start New Revision | Design Engineer |
| View (read-only) | Project Manager, All |

## Expected State Names

The client must use the selected authority mapping from GATE-A evidence (`docs/evidence/part-lifecycle-evidence.md`). State names must not be assumed from CAD or copied into a global enum. The current live map is evidence to resolve, not an approved configuration.
