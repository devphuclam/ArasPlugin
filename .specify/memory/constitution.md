# ArasPlugin Constitution

## Core Principles

### I. Repository and Sources of Truth

The canonical repository root is `ArasPlugin/`. Source code, compile-time contracts, and tests are authoritative for current behavior. Feature requirements, technical plans, and tasks belong respectively in `specs/<feature>/spec.md`, `specs/<feature>/plan.md`, and `specs/<feature>/tasks.md`.

### II. Architecture Boundaries

Respect dependency direction among Core, Workspace, Aras, Ui, Desktop, and IronCAD. Do not add dependency cycles. Keep Aras- and IronCAD-specific behavior outside Core unless an appropriate abstraction exists. Do not change namespaces, assemblies, solution, or project structure outside approved scope.

### III. Aras and Domain Safety

Never guess Aras ItemTypes, properties, relationships, lifecycles, permissions, or AML contracts. Aras schema changes require evidence from the schema map, source, tests, or verified documentation. Never log tokens, credentials, passwords, sessions, or sensitive data.

### IV. Compatibility

Preserve .NET Framework `net48`, Windows, WPF/WinForms, COM, strong-name, Aras IOM, and IronCAD compatibility. Do not change public behavior outside an approved specification.

### V. Testing and Verification

Every code change requires relevant tests or verification evidence. The repository baseline is `dotnet build IdeaCadConnector.sln` and `dotnet test IdeaCadConnector.sln`, currently expected to produce 0 warnings/0 errors and 645 passed, 0 failed, 0 skipped. Do not claim a command passed unless it was run.

### VI. Spec-Driven Development

Behavior-changing features use canonical Spec Kit artifacts. Do not create feature plans or tasks in `tasks/ai/`, `docs/plans/`, `docs/superpowers/`, or `.scratch/`. Small bugs, hotfixes, and chores may use the approved issue tracker.

### VII. Review and Documentation

The implementer does not self-approve. Reviewers compare changes with the governing spec or issue, and verifiers run build/test and record evidence. Synchronize canonical documentation when behavior, domain, architecture, or workflow changes; do not copy detailed documents into this constitution.

## Governance

This constitution governs repository work. Amendments require explicit approval, a documented rationale, and verification that affected instructions remain consistent. Environment failures must be distinguished from regressions. Destructive Git operations, production Aras changes, dependency installation, and legacy retirement require explicit approved scope.

**Version**: 1.0.0 | **Ratified**: 2026-07-16 | **Last Amended**: 2026-07-16
