# GATE-RW: Rework Side Effects

**Task**: T005c

**Requirement**: Verify the deployed side effects of `idea_RequestCadRework` on Part lifecycle and record the accepted MVP policy.

## Source Analysis

`src/IdeaCadConnector.Aras/ServerMethods/idea_RequestCadRework.cs`:
- Promotes CAD to `Thiet ke chi tiet`
- Then calls `Sync_Part_From_CAD`, which may load, unlock, promote, or version the linked Part
- Accepted by ADR-0012 as coordinated state-only behavior: Part and CAD retain separate lifecycle identities, while this rework operation returns both to `Thiet ke chi tiet` without creating a new engineering version.

## Live Read-Only Observation and Product Decision (2026-07-20)

The deployed `idea_RequestCadRework` source was inspected together with `Sync_Part_From_CAD`. The product owner confirmed the business result: Part returns to `Thiet ke chi tiet`, no new Part version is created, and duplicate Sync invocation in the rework path is a no-op. Result: **PASS for the accepted coordinated state-only rework policy by product owner confirmation.** The source-level helper behavior remains an implementation detail to re-verify after future server changes.

## Verification Required

- [x] Product owner confirmed CAD and Part return to `Thiet ke chi tiet`
- [x] Product owner confirmed no new Part version is created
- [x] Product owner confirmed duplicate Sync is a no-op
- [ ] Capture a retained live execution fixture/log for the deployed result
- [ ] Verify complete audit coverage for the rework transition (covered separately by GATE-N)

## Result

- Part lifecycle changed? **Yes — returns to `Thiet ke chi tiet`**
- Part version created? **No**
- Side effects: **Coordinated state-only update; duplicate Sync is a no-op**
- Evidence date: **2026-07-20**
- Environment: **IDEA live Aras environment**
- Verified by: **Product owner confirmation plus source/read-only inspection**

**Blocks**: No longer blocked by an unresolved business-policy decision. Retained live execution and audit evidence remain separate verification work; do not claim those as complete from this record alone.
