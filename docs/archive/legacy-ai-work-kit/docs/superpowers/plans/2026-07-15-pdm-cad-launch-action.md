# PDM CAD Launch Action UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore a persistent, standards-based PDM CAD primary action whose label and enabled state clearly distinguish Checkout, checked-out Open, and read-only Open.

**Architecture:** Introduce a pure `PdmCadLaunchActionState` factory as the single source of truth for visibility, mode, enabled state, label key, and disabled reason. `PdmProjectsViewModel` projects its existing live/local state through that factory, while XAML keeps one command and binds label plus tooltip to the computed presentation state.

**Tech Stack:** C#/.NET Framework 4.8, WPF/MVVM, existing localization dictionaries, xUnit.

## Global Constraints

- Do not add a second checkout command.
- Do not change Aras lock/download/check-in/cancel-checkout behavior.
- For a selected non-root node with a primary CAD reference, keep the primary action visible even when disabled.
- Root assembly rows and rows without primary CAD remain hidden.
- Disabled actions expose a localized actionable reason and do not rely on color alone.

---

### Task 1: Add a deterministic CAD launch action state

**Files:**
- Create: `src/IdeaCadConnector.Desktop/PdmCadLaunchActionState.cs`
- Create: `tests/IdeaCadConnector.Tests/PdmCadLaunchActionStateTests.cs`

**Interfaces:**
- Produces: `PdmCadLaunchMode` (`Hidden`, `Unavailable`, `CheckoutAndOpen`, `OpenCheckedOut`, `OpenReadOnly`).
- Produces: immutable `PdmCadLaunchActionState` with `Mode`, `IsVisible`, `IsEnabled`, `LabelKey`, and `DisabledReasonKey`.
- Produces: `PdmCadLaunchActionState.Create(PdmCadLaunchActionContext context)`.

- [ ] **Step 1: Write failing state-matrix tests**

Test hidden root/no-CAD rows; disconnected, missing-live-ID, and missing-state disabled modes; editable checkout; valid local checkout reopen; Released read-only; locked-by-other read-only; and locked-without-native disabled mode.

```csharp
var state = PdmCadLaunchActionState.Create(new PdmCadLaunchActionContext
{
    HasSelection = true,
    HasPrimaryCad = true,
    IsConnected = true,
    HasLiveCadId = true,
    HasLifecycleState = true,
    CanCheckout = true
});
Assert.Equal(PdmCadLaunchMode.CheckoutAndOpen, state.Mode);
Assert.True(state.IsVisible);
Assert.True(state.IsEnabled);
```

- [ ] **Step 2: Run tests and verify RED**

Run:
`dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj --filter FullyQualifiedName~PdmCadLaunchActionStateTests`

Expected: compile failure because the state types do not exist.

- [ ] **Step 3: Implement the pure state factory**

Apply precedence: hidden structural rows; unavailable prerequisites; valid checkout; other-user lock; editable checkout; non-editable read-only; unavailable file. Return localization keys rather than literal UI text.

- [ ] **Step 4: Run tests and verify GREEN**

Run the command from Step 2. Expected: all matrix tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/IdeaCadConnector.Desktop/PdmCadLaunchActionState.cs tests/IdeaCadConnector.Tests/PdmCadLaunchActionStateTests.cs
git commit -m "feat(pdm): model CAD launch action states"
```

### Task 2: Bind PDM Projects UI to the launch state

**Files:**
- Modify: `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs`
- Modify: `src/IdeaCadConnector.Desktop/PdmProjectsView.xaml`
- Modify: `src/IdeaCadConnector.Core/Localization/TranslationKeys.cs`
- Modify: `src/IdeaCadConnector.Core/Localization/TranslationResources.cs`
- Create: `tests/IdeaCadConnector.Tests/PdmCadLaunchActionUiTests.cs`

**Interfaces:**
- Consumes: `PdmCadLaunchActionState.Create(...)` from Task 1.
- Produces: `CadLaunchActionState`, `OpenInIronCadModeText`, `OpenInIronCadToolTip`, `HasOpenInIronCadAction`, and `CanOpenInIronCad` as consistent state projections.

- [ ] **Step 1: Write failing view-model/XAML/localization tests**

Assert that selected actionable CAD rows remain visible while unavailable, labels map to Checkout/Open/Read-only keys, tooltip maps disabled reasons, `RefreshCanOpenInIronCad` raises all dependent properties, and XAML binds `ToolTip` without changing `OpenInIronCadCommand`.

```csharp
Assert.Contains("ToolTip=\"{Binding OpenInIronCadToolTip}\"", xaml);
Assert.Contains("PdmCheckoutAndOpenIronCad", translationKeysSource);
Assert.DoesNotContain("CheckoutCommand", pdmProjectsXaml);
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:
`dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj --filter "FullyQualifiedName~PdmCadLaunchAction"`

Expected: failures for missing properties, localization keys, and tooltip binding.

- [ ] **Step 3: Integrate state into the view model**

Build context from `SelectedNode`, root status, `MainViewModel.SharedArasCadClient`, `_liveCadId`, `_liveCadState`, `_liveHasNativeFile`, `IsCheckedOutByMe`, `IsCheckedOutByOther`, and `IsOpeningInIronCad`. Replace independent visibility/enablement/text expressions with projections of the computed state. Raise property changes whenever the existing `RefreshCanOpenInIronCad` path runs.

- [ ] **Step 4: Add localization and XAML tooltip**

Add English, Vietnamese, and Japanese entries for Checkout & Open, Open checked-out CAD, Open read-only, and each disabled prerequisite. Bind the existing button's `ToolTip` to `OpenInIronCadToolTip`; retain the existing command and accessibility behavior.

- [ ] **Step 5: Run focused and full verification**

```powershell
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj --filter "FullyQualifiedName~PdmCadLaunchAction"
dotnet test tests/IdeaCadConnector.Tests/IdeaCadConnector.Tests.csproj
dotnet build src/IdeaCadConnector.Desktop/IdeaCadConnector.Desktop.csproj
```

Expected: focused tests pass, full suite has zero failures, and Desktop build exits 0 with zero errors.

- [ ] **Step 6: Commit**

```powershell
git add src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs src/IdeaCadConnector.Desktop/PdmProjectsView.xaml src/IdeaCadConnector.Core/Localization/TranslationKeys.cs src/IdeaCadConnector.Core/Localization/TranslationResources.cs tests/IdeaCadConnector.Tests/PdmCadLaunchActionUiTests.cs
git commit -m "fix(pdm): restore persistent checkout action UX"
```
