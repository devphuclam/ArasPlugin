# Phase 3 Closeout Plan

## Remaining Before Phase 3 COMPLETE

1. Sprint 3.4 Production Readiness UAT accepted by user.
2. Final package validation passed.
3. Final go/no-go decision made.
4. Docs-only Phase 3 closeout after user confirms UAT pass.
5. Optional Git tag created.

## Step 1 — Sprint 3.4 Production Readiness UAT

Run:

- Build solution (Debug + Release).
- Run tests (Debug + Release).
- Run package script.
- Run validation script.
- Run verification script.
- User confirms acceptance.

## Step 2 — Final Package Validation

Run validation script against zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\release\validate-release-package.ps1 -PackagePath .\artifacts\release\IdeaCadConnector-v0.3.0-rc1.zip -ExpectedVersion v0.3.0-rc1
```

Require exit code 0.

## Step 3 — Final Go/No-Go Decision

Fill in [GO-NO-GO-CHECKLIST.md](GO-NO-GO-CHECKLIST.md). Decision must be GO or GO WITH ACCEPTED LIMITATIONS.

## Step 4 — Docs-Only Phase 3 Closeout

After user confirms:

- Update `docs/part-library/STATUS.md`: Phase 3 -> `COMPLETE`.
- Update `docs/part-library/phase-3/README.md`: Sprint 3.4 -> accepted; Phase 3 -> `COMPLETE`.
- Update `docs/part-library/phase-3/ACCEPTANCE.md`: Sprint 3.4 UAT accepted; Phase 3 -> `COMPLETE`.
- Update `docs/part-library/README.md`: Phase 3 row -> `COMPLETE`.
- Compact phase-3 to canonical files: DESIGN.md, DEPLOYMENT.md, ACCEPTANCE.md, FINAL-STATUS.md.
- Remove non-canonical sprint-specific files after merging durable content.
- Record exact completion commit SHA.

## Step 5 — Optional Git Tag

```powershell
git tag v0.3.0-rc1
```

Do not create tag inside Sprint 3.4 unless user explicitly asks after UAT acceptance.

## Closeout Criteria

- [ ] Sprint 3.4 UAT accepted
- [ ] Build/test/package/validate all pass
- [ ] Go/No-Go decision = GO or GO WITH ACCEPTED LIMITATIONS
- [ ] Known limitations documented and accepted
- [ ] No P0/P1 blockers
- [ ] Phase 3 docs updated to COMPLETE state
- [ ] Git tag created (optional, user decision)
- [ ] Phase 3 closeout reported to user
