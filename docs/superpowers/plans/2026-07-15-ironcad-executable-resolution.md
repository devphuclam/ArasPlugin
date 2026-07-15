# IronCAD Executable Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Checkout open downloaded CAD files with the installed IronCAD automatically when no executable path is configured.

**Architecture:** Add a focused desktop resolver that applies configured, running-process, registry, and versioned-install candidates in order. Inject the resolver into the external adapter and open service so availability checks and process launch use identical resolution rules.

**Tech Stack:** C#/.NET Framework 4.8, WPF desktop, `System.Diagnostics`, `Microsoft.Win32`, xUnit.

## Global Constraints

- Do not change Aras lock, Vault download/upload, check-in, cancel-checkout, lifecycle, or authorization behavior.
- Accept only an existing local file named `IRONCAD.exe`.
- Invalid configured paths fall through to discovery.
- Discovery errors are non-fatal; a genuine no-install result keeps the existing user-facing failure.

---

### Task 1: Add the executable resolver

**Files:**
- Create: `src/IdeaCadConnector.Desktop/IronCadExecutableResolver.cs`
- Create: `tests/IdeaCadConnector.Tests/IronCadExecutableResolverTests.cs`

**Interfaces:**
- Produces: `internal sealed class IronCadExecutableResolver` with `string Resolve(string configuredPath)`.
- Consumes: injected `Func<IEnumerable<string>>` providers for running process, registry, and install candidates; the parameterless constructor wires Windows discovery.

- [ ] **Step 1: Write failing resolver tests**

Cover configured-path precedence, invalid-config fallback, source ordering, candidate filename validation, all-provider exception isolation, and highest version install ordering. Use temporary files named `IRONCAD.exe` and injected providers; do not depend on the developer workstation.

```csharp
var resolver = new IronCadExecutableResolver(
    () => new[] { runningPath },
    () => new[] { registryPath },
    () => new[] { installPath });
Assert.Equal(configuredPath, resolver.Resolve(configuredPath));
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:
`dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj --filter FullyQualifiedName~IronCadExecutableResolverTests`

Expected: compile failure because `IronCadExecutableResolver` does not exist.

- [ ] **Step 3: Implement minimal resolver**

Implement validation and ordered providers. The default providers must:

```csharp
Process.GetProcessesByName("IRONCAD")
Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\IRONCAD.exe")
Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
```

For install directories, enumerate `IronCAD/*/bin/IRONCAD.exe`, parse directory names with `Version.TryParse`, and return highest versions first. Dispose process and registry objects. Catch discovery exceptions per provider and continue.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all resolver tests pass.

- [ ] **Step 5: Commit resolver**

```powershell
git add src/IdeaCadConnector.Desktop/IronCadExecutableResolver.cs tests/IdeaCadConnector.Tests/IronCadExecutableResolverTests.cs
git commit -m "fix(desktop): discover installed IronCAD executable"
```

### Task 2: Use one resolution policy for Checkout launch

**Files:**
- Modify: `src/IdeaCadConnector.Desktop/IronCadExternalAdapter.cs`
- Modify: `src/IdeaCadConnector.Desktop/Services/IronCadOpenService.cs`
- Modify: `src/IdeaCadConnector.Desktop/IronCadAdapterFactory.cs`
- Modify: `tests/IdeaCadConnector.Tests/IronCadOpenServiceTests.cs`
- Modify: `tests/IdeaCadConnector.Tests/PdmIronCadAdapterTests.cs`

**Interfaces:**
- Consumes: `IronCadExecutableResolver.Resolve(string configuredPath)` from Task 1.
- Produces: blank or stale configuration no longer blocks factory creation, availability, or launch when discovery finds IronCAD.

- [ ] **Step 1: Write failing integration tests**

Add tests proving an invalid/null configured path falls back to an injected discovered executable for both `IronCadExternalAdapter` and `IronCadOpenService`, and that `IronCadAdapterFactory.Create(null)` returns an adapter instead of throwing.

```csharp
var resolver = new IronCadExecutableResolver(
    () => Array.Empty<string>(),
    () => Array.Empty<string>(),
    () => new[] { discoveredPath });
var adapter = new IronCadExternalAdapter(null, resolver);
Assert.Equal(discoveredPath, adapter.ResolvedExecutablePath);
```

- [ ] **Step 2: Run integration tests and verify RED**

Run:
`dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj --filter "FullyQualifiedName~IronCadOpenServiceTests|FullyQualifiedName~PdmIronCadAdapterTests"`

Expected: compile/assertion failures because resolver injection and fallback factory behavior are absent.

- [ ] **Step 3: Integrate the resolver**

In `IronCadExternalAdapter`, resolve immediately before launch and expose the resolved path internally for tests. In `IronCadOpenService`, use the resolver in `IsIronCadAvailable`. In `IronCadAdapterFactory`, remove the blank-path exception and always create `IronCadExternalAdapter`; genuine absence is reported only when opening.

- [ ] **Step 4: Run focused tests and full verification**

Run:

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj --filter "FullyQualifiedName~IronCadOpenServiceTests|FullyQualifiedName~PdmIronCadAdapterTests"
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj
dotnet build src/IdeaCadConnector.Desktop/IdeaCadConnector.Desktop.csproj
```

Expected: focused tests pass, full suite has zero failures, and Desktop build exits 0.

- [ ] **Step 5: Verify this workstation and commit**

Resolve without configuration and assert the result is `C:\Program Files\IronCAD\2025\bin\IRONCAD.exe`; then commit:

```powershell
git add src/IdeaCadConnector.Desktop/IronCadExternalAdapter.cs src/IdeaCadConnector.Desktop/Services/IronCadOpenService.cs src/IdeaCadConnector.Desktop/IronCadAdapterFactory.cs tests/IdeaCadConnector.Tests/IronCadOpenServiceTests.cs tests/IdeaCadConnector.Tests/PdmIronCadAdapterTests.cs
git commit -m "fix(desktop): open checkout with discovered IronCAD"
```
