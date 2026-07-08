# Environment Configuration

## Current Configuration State

The desktop app currently has no `src/IdeaCadConnector.Desktop/App.config`.

The desktop project copies this config-like runtime file to output:

```text
src/IdeaCadConnector.Desktop/pdm-naming-policy.json
```

Aras connection and login values are currently supplied at runtime/session level. Sprint 3.1 does not refactor startup configuration.

## Required Environment Values

Internal release UAT needs these values confirmed before testing:

| Value | Required For |
|---|---|
| Aras base URL | login, Open in Aras, API calls |
| Database name | login/session selection |
| Authentication mode | normal user login flow |
| Vault/cache location | CAD download/open flow |
| IronCAD executable or file association | Open in IronCAD |
| Browser behavior | Open in Aras |
| Role identities | Library authorization UAT |

## Role Identities

| ID | Role |
|---|---|
| `TPTKC` | Truong phong thiet ke co |
| `TNTKC` | Truong nhom thiet ke co |
| `NVTKC` | Nhan vien thiet ke co |
| `NVLCR` | Nhan vien lap rap co |
| `PM` | Quan ly du an |
| `Customer` | Khach hang |

## Template

Sprint 3.1 includes a documentation-only template:

```text
templates/connection.template.json
```

The app does not load this file automatically yet. External configuration loading is a Sprint 3.2 candidate: Environment Configuration Hardening.
