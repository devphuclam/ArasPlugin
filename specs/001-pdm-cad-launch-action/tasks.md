# PDM CAD Launch Action Tasks

Migration tasks are historical completed work unless explicitly marked open.

## Completed historical implementation

- [x] T001 Implement deterministic CAD launch action state and state matrix — Evidence: commit `18de2f5`; `PdmCadLaunchActionStateTests`.
- [x] T002 Integrate state projections into PDM ViewModel and preserve the existing command — Evidence: commit `07cf495`; `PdmProjectsViewModel.cs`, `PdmProjectsView.xaml`.
- [x] T003 Add localized labels, disabled reasons, and tooltip binding — Evidence: commit `07cf495`; `TranslationResources.cs`, `PdmCadLaunchActionUiTests`.

## Completed verification

- [x] T004 Verify focused state and UI tests — Evidence: `PdmCadLaunchActionStateTests.cs`, `PdmCadLaunchActionUiTests.cs`.
- [x] T005 Verify full solution build and test baseline after migration — Evidence: current repository verification; latest approved output must be recorded by verifier.

## Open work

No open implementation task was reconstructed from the approved historical evidence. Manual IronCAD smoke testing remains outside this documentation migration and is not converted into a fabricated task.
