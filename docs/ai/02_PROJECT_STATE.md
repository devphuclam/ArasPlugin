# 02 — Project State

_Last audit source: uploaded repository archive, 2026-07-10._

## Repository

- Solution: `IdeaCadConnector.sln`
- Framework: .NET Framework 4.8
- Desktop: WPF/MVVM
- CAD focus: IronCAD `.ics`
- Aras communication: AML/HTTP/Vault and server-side C# Methods
- Snapshot commit observed in archive: `29e2ef364eaa3106c3fba76a3da5b0d5bcdf0eba`

## Important warning

The uploaded archive contained a dirty working tree with many modified files. Do not assume those changes are intentional or safe. `BASE-00` must establish a clean, backed-up baseline before an agent edits source.

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
- Next ticket: `BASE-00`.
- Do not begin: `DOC-03`, Pull or Branch before `BASE-04` is completed.

## Update discipline

The Verifier updates this document after each merged ticket:

- current main commit;
- completed ticket;
- test baseline;
- newly confirmed schema;
- known blocker;
- next allowed tickets.
