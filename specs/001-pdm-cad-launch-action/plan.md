# PDM CAD Launch Action Technical Plan

## Migration mode

Historical implementation reconstructed from verified evidence.

## Existing architecture

`PdmProjectsViewModel` owns the selected PDM presentation state and existing Open-in-IronCAD command. `PdmCadLaunchActionState` is the pure state model/factory. `PdmProjectsView.xaml` binds the existing action. Core localization resources provide labels and reasons. Tests cover state, UI binding, and existing CAD services.

## Components and files involved

- `src/IdeaCadConnector.Desktop/PdmCadLaunchActionState.cs`
- `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs`
- `src/IdeaCadConnector.Desktop/PdmProjectsView.xaml`
- `src/IdeaCadConnector.Core/Localization/TranslationKeys.cs`
- `src/IdeaCadConnector.Core/Localization/TranslationResources.cs`
- `tests/IdeaCadConnector.Tests/PdmCadLaunchActionStateTests.cs`
- `tests/IdeaCadConnector.Tests/PdmCadLaunchActionUiTests.cs`

## Technical decisions evidenced by implementation

- A single pure presentation state is the source of truth for mode, visibility, enablement, label, and disabled reason.
- The existing Open-in-IronCAD command remains the command boundary.
- Localization keys are used instead of literal UI labels.
- Root/no-CAD rows are structurally hidden; actionable rows with incomplete prerequisites remain visible but disabled.

## Compatibility constraints

Preserve .NET Framework `net48`, WPF/MVVM bindings, existing Aras lock/download/check-in/cancel behavior, existing CAD adapter contracts, and localization behavior. No source evidence supports changing Aras schema or remote protocol behavior for this feature.

## Verification strategy

- Inspect state-matrix tests and UI binding tests.
- Run focused `PdmCadLaunchAction` tests.
- Run the full solution test suite.
- Build the Desktop project and solution.
- Compare the reconstructed artifact with commits `18de2f5` and `07cf495`.

## Legacy traceability

Source design and plan are retained under `docs/archive/legacy-ai-work-kit/docs/superpowers/`. The implementation evidence is the two historical commits and the current source/test files listed above.
