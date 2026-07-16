# Implementer Prompt

Implement only the approved plan.

Rules:

- Keep the diff minimal.
- Do not invent Aras/IronCAD APIs.
- Add meaningful tests with the code.
- Preserve backward compatibility or add a migration.
- Propagate CancellationToken.
- Do not log secrets.
- Do not update manifest/commit/branch state before full success.
- Stop and report BLOCKED on schema uncertainty or destructive risk.

At the end return behavior before/after, files changed, commands, results, limitations and acceptance-criteria mapping.
