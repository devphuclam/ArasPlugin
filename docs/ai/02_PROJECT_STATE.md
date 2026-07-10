# 02 — Project State

_Last audit source: uploaded repository archive, 2026-07-10._

## Repository

- Solution: `IdeaCadConnector.sln`
- Framework: .NET Framework 4.8
- Desktop: WPF/MVVM
- CAD focus: IronCAD `.ics`
- Aras communication: AML/HTTP/Vault and server-side C# Methods
- Snapshot commit observed in archive: `29e2ef364eaa3106c3fba76a3da5b0d5bcdf0eba`
- Current baseline commit (HEAD): `443847ff577710705879bbdd06013e1b58f1af03` (includes AI Work Kit + OpenCode + PROJECT_STATE.md update)
- Clean source baseline tag: `baseline/clean-source` → `1c8a1b99672f5c791aa299dbebd70360503a71c3` (source code without AI infrastructure)
- AI Work Kit baseline tag: `baseline/with-ai-work-kit` → `443847f` (HEAD including all AI governance files)

## Important warning — resolved by BASE-00

BASE-00 established a clean, backed-up baseline. The original dirty working tree from the uploaded archive has been documented and isolated. Working tree is now clean. Do not assume any pre-BASE-00 uncommitted changes are intentional or safe.

## Confirmed existing capabilities

- Login/logout and Part search.
- Checkout, read-only open, Check-in and cancel checkout.
- CAD native-file Vault upload/download.
- CAD lifecycle/workflow actions.
- PDM folder analyze and push preview.
- Push Part, BOM, CAD and Document metadata.
- Clone Part/BOM and real CAD file.
- Document clone currently creates zero-byte placeholders.
- Local branches and non-main staging behavior.
- PDM Commit best-effort support.
- Start New Revision client and `idea_ReviseCad` reference server method.
- Part Library creation, management, revision policy, reuse and usage tracking.

## Confirmed gaps targeted by this roadmap

- Pull command is not connected to real synchronization.
- Physical Document file upload/attachment/download is incomplete.
- Commit file history lacks complete per-file diff/author/parent/Vault linkage.
- Branch is not a complete server-side snapshot branch model.
- Several sidebar destinations/settings/reports remain incomplete.

## Current execution phase

- Phase: Baseline and schema verification.
- Next ticket: `BASE-01`.
- Do not begin: `DOC-03`, Pull or Branch before `BASE-04` is completed.

## Update discipline

The Verifier updates this document after each merged ticket:

- current main commit;
- completed ticket;
- test baseline;
- newly confirmed schema;
- known blocker;
- next allowed tickets.

## BASE-00 completion record

- Completed: 2026-07-10
- HEAD commit: `443847ff577710705879bbdd06013e1b58f1af03`
- Build: Succeeded (0 warnings, 0 errors)
- Test: 419 passed, 0 failed, 0 skipped (4 s)
- Tags created: `baseline/clean-source`, `baseline/with-ai-work-kit`
- Working tree: clean
- Next ticket: `BASE-01` (build baseline)
