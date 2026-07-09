# Machine Readiness

> **Sprint 3.3 Internal Installation Package UAT accepted** on 2026-07-09.
> See [ACCEPTANCE.md](ACCEPTANCE.md).

## Minimum Requirements

| Requirement | Details |
|---|---|
| Windows | Windows 10 or Windows Server 2016+ (x64) |
| .NET Framework | 4.8 (installed or enabled via Windows Features) |
| Network | Access to the target Aras Innovator server URL |
| Browser | Default browser for Open-in-Aras links |
| Aras account | Valid login with appropriate permissions |
| Aras method deployed | `idea_GetPrimaryIronCadForPart` |
| Disk space | ~200 MB for app + vault cache |

## Optional Requirements

| Requirement | For Testing |
|---|---|
| IronCAD installed | Open in IronCAD flow |
| Vault read permission | CAD download flow |
| File write permission on cache folder | CAD vault cache |

## Role Identities

The tester must belong to one of the following organization role identities:

| ID | Role | Capability |
|---|---|---|
| `TPTKC` | Trưởng phòng thiết kế cơ | Manager — full access |
| `TNTKC` | Trưởng nhóm thiết kế cơ | Reviewer — move/pin allowed |
| `NVTKC` | Nhân viên thiết kế cơ | Contributor — view/use only |
| `NVLCR` | Nhân viên lắp ráp cơ | Assembly viewer — read-only |
| `PM` | Quản lý dự án | Project viewer — read-only |
| `Khách hàng` | Customer | External viewer — read-only |

## Aras Permission Checklist

Before testing, confirm the user's Aras identity has:

- Execute Method permission for `idea_GetPrimaryIronCadForPart`
- Get permission for `Part`, `CAD`, `Part CAD`, `File`
- Get permission for `idea_PartLibrary`, `idea_PartLibraryEntry`
- Get permission for BOM relationships if Where Used tab is needed
- Vault download permission if CAD download is tested

## Verification Steps

1. Open `https://your-aras-server/InnovatorServer` in a browser — confirm reachable.
2. Confirm `.NET Framework 4.8` is installed:
   ```powershell
   Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" | Select-Object Release, Version
   ```
3. Confirm the Aras method exists in the target database (admin can check Method list).
