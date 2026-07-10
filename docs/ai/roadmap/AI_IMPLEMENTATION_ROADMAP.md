# AI Implementation Roadmap

## Release 0 — Safe baseline

`BASE-00 → BASE-01 → BASE-02 → BASE-04 → BASE-05`

## Release 1 — Document Complete

`DOC-01 → DOC-02 → DOC-03 → DOC-04 → DOC-05 → DOC-06 → DOC-07 → DOC-08`

Outcome: physical Document file can be uploaded, attached, versioned and downloaded; placeholder is legacy compatibility only.

## Release 2 — Sync Complete

`WSP-01..07 → COM-01..07 → PULL-01..11`

Outcome: manifest v2, deterministic diff, commit history, Pull preview, conflict handling, backup and rollback.

## Release 3 — Branch Complete

`BR-01..09`

Outcome: remote branch schema, branch head, branch-specific Clone/Pull/Push, safe switch and promote.

## Release 4 — Production Ready

`UI-01..07 + OPS-01..09`

Outcome: usable navigation/settings/reports, structured operations, deployable Aras package, UAT and release docs.

## Parallelism

Safe parallel work is limited:

- UI mockup-only tickets can run after their contracts stabilize.
- OPS logging/redaction may run alongside later Pull tickets if it does not alter business behavior.
- Never parallelize two tickets editing the same public contract or `HttpPdmRepositoryClient`.
