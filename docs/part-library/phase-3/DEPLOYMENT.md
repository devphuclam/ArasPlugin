# Phase 3 Deployment

## Release Candidate

```text
IdeaCadConnector v0.3.0-rc1
```

## Install Package Contents

The release zip contains:

- desktop app binaries under `app/`;
- required Aras method source under `aras/server-methods/`;
- Aras deployment notes under `aras/README-Aras-Deployment.md`;
- install, configuration, UAT, rollback, and release note docs under `docs/`;
- package checksums under `checksums/SHA256SUMS.txt`;
- package metadata under `VERSION.txt`.

## Internal Install Steps

1. Extract the zip to an internal user-writable folder.
2. Open `docs/CONFIGURATION.md` and confirm the target Aras base URL, database, login mode, and local IronCAD availability.
3. Confirm `aras/server-methods/idea_GetPrimaryIronCadForPart.cs` is deployed to the target Aras database.
4. Run `app/IdeaCadConnector.Desktop.exe`.
5. Sign in to Aras.
6. Run the UAT checklist in `docs/UAT-CHECKLIST.md`.

## Aras Prerequisites

Required method:

```text
idea_GetPrimaryIronCadForPart
```

Method type: C#

Behavior: read-only Part-to-primary-IronCAD-CAD lookup.

Input:

```text
part_id
```

Output:

```text
id,item_number,name,classification,authoring_tool,generation,state,locked_by_id,native_file
```

Required permissions:

- Execute Method;
- Get Part;
- Get CAD;
- Get Part CAD relationship;
- Get File/native_file metadata and Vault file where download is tested.

The method must not add, edit, promote, create relationships, upload files, or mutate live data.

## Organization Role Identities

| ID | Role |
|---|---|
| `TPTKC` | Truong phong thiet ke co |
| `TNTKC` | Truong nhom thiet ke co |
| `NVTKC` | Nhan vien thiet ke co |
| `NVLCR` | Nhan vien lap rap co |
| `PM` | Quan ly du an |
| `Customer` | Khach hang |

## Rollback

Rollback is file-based for Sprint 3.1. See [ROLLBACK.md](ROLLBACK.md).
