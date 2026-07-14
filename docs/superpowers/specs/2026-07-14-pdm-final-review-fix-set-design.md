# PDM Final Review Fix Set Design

## Goal

Close the six final-review findings in normalized PDM publication and manifest import without changing the intended replacement workflow, legacy folder analysis, or IronCAD source documents.

## Publication path safety

`PdmNameNormalizer.NormalizeProjectCode` rejects normalized values that are `.` or `..` and every value whose first character is not ASCII alphanumeric. `PdmPackagePublicationPaths` canonicalizes the output directory and proves that final and pending are distinct direct children of it.

`PdmPackagePublicationTransaction` treats that common canonical parent as the publication root. It validates the relationship at construction and revalidates immediately before every recursive pending/final deletion. Existing reparse points in the publication root, final path, pending path, or their existing ancestors are rejected so a junction or symbolic-link alias cannot redirect recursive deletion.

The IronCAD command constructs the publication transaction before any package write. Every failure before publication rolls back pending through the guarded transaction while leaving an existing final package untouched. Final rollback is permitted only after `CommitPendingReplacingFinal` has completed and marked the new final as published.

## IronCAD dependency discovery

Only the known unavailable-property COM failure from `IZSceneElement.ModelLinkPath` is suppressed. `GetChildrenZArray` failures are wrapped as dependency-discovery failures and propagate, so incomplete scene traversal fails closed. Tests cover the exception classification and source contract without requiring an IronCAD runtime.

## Manifest import integrity

`PdmBusinessNode` carries a quantity with a default of one for legacy producers. The manifest reader resolves each occurrence quantity from the BOM-v2 edge identified by parent occurrence and child definition, then Desktop tree construction and `PushPreviewMapper` preserve that value.

Duplicate occurrence IDs remain validation failures and become blocking naming issues. Cycle analysis and business mapping use deterministic first-occurrence projections instead of duplicate-key `ToDictionary` operations, allowing malformed packages to remain inspectable without throwing.

## Verification

Regression tests cover unsafe normalized project codes, direct-child publication boundaries, reparse-point rejection and target preservation, pre-publication pending cleanup with old-final preservation, fail-closed child enumeration, quantity greater than one through reader/Desktop/push mapping, and duplicate occurrence IDs through the package reader. Each production change follows a witnessed red test. Focused tests run after each cycle, followed by the complete Debug test suite.
