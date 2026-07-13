# SEC-00-HOTFIX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair SEC-00 configuration regressions and unsafe tests without changing schema, server methods, DOC-03, or Git history.

**Architecture:** Keep the existing configuration/factory architecture and add narrow internal seams. The loader gets an injectable candidate-path context, options owns canonical BaseUri normalization, HTTP auth uses option values through its existing HTTP client, and PDM uses one adapter-construction helper reading configured IronCAD path.

**Tech Stack:** C#, .NET Framework 4.8, Newtonsoft.Json, xUnit, `dotnet` CLI, PowerShell verification scripts.

## Global Constraints

- Do not modify or deploy Aras schema or server methods.
- Do not begin DOC-03 or modify Document Vault behavior.
- Do not rewrite Git history, force-push, push directly to `main`, merge, or delete user data.
- Never make tests write to real AppData, application output, or repository-local config paths.
- Every production behavior change is preceded by a failing regression test.
- Do not reproduce previously exposed infrastructure values in new files or reports.

---

### Task 1: Isolated configuration path tests

**Files:**
- Modify: `src/IdeaCadConnector.Core/Configuration/EnvironmentConfigurationLoader.cs`
- Modify: `tests/IdeaCadConnector.Tests/EnvironmentConfigurationTests.cs`

**Interfaces:**
- Produces an internal loader path context containing environment-variable value, side-by-side directory, and AppData directory.
- Preserves public `Load()`/`ResolvePath()` behavior while tests call the internal overload.

- [ ] **Step 1: Write failing tests** for temp-only env precedence, side-by-side precedence, AppData fallback, sentinel preservation, env restoration, factory reset, and explicit invalid env-path errors.
- [ ] **Step 2: Run only the new tests** with `dotnet test ... --filter FullyQualifiedName~EnvironmentConfigurationTests...`; confirm failures are caused by the current real-path/static-state behavior.
- [ ] **Step 3: Implement the minimal internal path context and authoritative env-path handling.** Blank env values retain fallback; nonblank values require an existing readable file and produce a controlled error otherwise.
- [ ] **Step 4: Run the same tests** and confirm GREEN; inspect temp-root cleanup and prove sentinel remains.
- [ ] **Step 5: Commit** `test/fix(SEC-00-HOTFIX): isolate configuration path resolution`.

### Task 2: BaseUri normalization and HTTP OAuth configuration

**Files:**
- Modify: `src/IdeaCadConnector.Aras/ArasClientOptions.cs`
- Modify: `src/IdeaCadConnector.Aras/ArasAuthenticator.cs`
- Modify: `src/IdeaCadConnector.Aras/HttpArasCadClient.cs`
- Modify: `src/IdeaCadConnector.Core/Configuration/EnvironmentConfiguration.cs` only if validation result plumbing requires it
- Modify: `tests/IdeaCadConnector.Tests/EnvironmentConfigurationTests.cs`
- Modify or create: focused HTTP authentication test seam/test file under `tests/IdeaCadConnector.Tests`

**Interfaces:**
- `ArasClientOptions` exposes one internal/static normalization routine used by configuration mapping and login overrides.
- Token requests use `_options.OAuthClientId` and `_options.OAuthScope`.

- [ ] **Step 1: Write failing tests** for one/multiple trailing slash normalization, HTTP/HTTPS-only schemes, token and Vault endpoint resolution, login override normalization, OAuth form values, and missing OAuth validation.
- [ ] **Step 2: Run only those tests** and confirm RED against unnormalized BaseUri, concatenated authenticator endpoint, and hardcoded HTTP OAuth fields.
- [ ] **Step 3: Implement normalization, URI-relative endpoint construction, option validation, and configured OAuth form fields.** Preserve safe defaults only where existing behavior intentionally supports them.
- [ ] **Step 4: Run focused tests** and confirm GREEN without live network access.
- [ ] **Step 5: Commit** `fix(SEC-00-HOTFIX): normalize Aras endpoints and OAuth settings`.

### Task 3: PDM IronCAD adapter propagation

**Files:**
- Modify: `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs`
- Modify: narrowly related adapter/factory interface only if required by the smallest seam
- Modify: `tests/IdeaCadConnector.Tests/EnvironmentConfigurationTests.cs` or a focused PDM test file

**Interfaces:**
- One private helper/factory in `PdmProjectsViewModel` creates `IronCadExternalAdapter` with the configured `IronCadExecutablePath`.

- [ ] **Step 1: Write failing tests** proving configured path is used by edit, read-only, checkout/download, and all PDM construction paths; add a source-level guard that no parameterless production construction remains.
- [ ] **Step 2: Run the focused tests** and confirm RED because current PDM sites construct adapters without a path.
- [ ] **Step 3: Implement the narrow helper and replace all seven PDM construction sites.** Preserve lifecycle/checkout/read-only behavior and surface missing-path failures through existing operation handling.
- [ ] **Step 4: Run focused tests** and confirm GREEN.
- [ ] **Step 5: Commit** `fix(SEC-00-HOTFIX): propagate configured IronCAD path to PDM`.

### Task 4: Current-tree sanitation and status evidence

**Files:**
- Modify: `tasks/ai/tickets/SEC-00-HOTFIX-repair-config-regressions.md`
- Modify: `docs/ai/02_PROJECT_STATE.md`
- Modify: `tasks/ai/tickets/SEC-00-externalize-aras-environment-config.md`
- Modify: `tasks/ai/tickets/BASE-04-verify-aras-schema-map.md`
- Modify: `docs/part-library/phase-3/templates/connection.template.json`
- Modify: tests/comments containing exposed infrastructure values, replacing them with synthetic values/placeholders

- [ ] **Step 1: Run the required current-tree scans** and record exact matches without copying sensitive values into new documents.
- [ ] **Step 2: Replace current tracked values** with placeholders/synthetic examples; update assertions to test safe properties rather than asserting against exposed strings.
- [ ] **Step 3: Add the hotfix ticket and correction record** stating historical commits are outside scope and verification claims require fresh output.
- [ ] **Step 4: Re-run scans** and confirm no hotfix-caused sensitive matches remain.
- [ ] **Step 5: Commit** `docs(SEC-00-HOTFIX): sanitize current configuration evidence`.

### Task 5: Full verification and handoff

**Files:**
- Modify only verification evidence/ticket/project-state files when commands produce fresh results.

- [ ] **Step 1: Run** restore, Debug/Release builds, Debug/Release tests, `Check-AiScope`, and `Verify-AiTicket -TicketId SEC-00-HOTFIX`.
- [ ] **Step 2: Inspect** `git status`, `git diff --check main...HEAD`, diff stat/name-status, tracked artifact scan, and complete diff for unrelated changes/mojibake/generated files.
- [ ] **Step 3: Record exact counts and any independent script failure** without claiming success for failed verification.
- [ ] **Step 4: Commit final evidence** only after all hotfix-caused checks pass.
- [ ] **Step 5: Push the branch and create a draft PR into `main` only if GitHub permissions allow; never merge.**
