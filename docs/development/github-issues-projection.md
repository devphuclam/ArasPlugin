# GitHub Issues Projection Policy

## Canonical ownership

Spec Kit `tasks.md` is canonical. A GitHub Issue is an execution projection for eligible open work; it is not a second requirements source.

## Projection rules

- Project only reviewed unchecked (`[ ]`) tasks.
- Never project completed (`[x]`) historical tasks.
- Before creating an issue, search for the marker to prevent duplicates:

  `Spec-Kit-Task: <feature-slug>#<task-id>`

- Each projected issue links back to the feature directory, `spec.md`, `plan.md`, and task ID.
- Add the issue URL beside the task only after successful projection.
- Never reopen completed legacy work automatically.

## Pilot result

Pilot: `specs/001-pdm-cad-launch-action/`.

Result: `No eligible open task for projection`. All reconstructed implementation and verification tasks are completed (`[x]`), so no GitHub Issue was created.

## Tooling status

GitHub CLI is not currently available. `winget install --id GitHub.cli --exact --accept-source-agreements --accept-package-agreements` located GitHub CLI `2.96.0`, verified the MSI hash, then the installer was cancelled and returned exit code `1602`. A subsequent `gh --version` check returned `GH_MISSING_AFTER_INSTALL_ATTEMPT`.

Authentication and repository verification remain blocked until GitHub CLI is installed and the user completes interactive authentication:

```powershell
gh auth login
gh auth status
gh repo view
```

No token, credential, or auth output is stored in this repository.
