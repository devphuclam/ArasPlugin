# Part/CAD Versioning Discipline Evidence

## Scope

- Feature: `003-controlled-cad-design-release`
- Date: 2026-07-21
- Environment: Aras Innovator live instance used for the controlled fixture
- Fixture: Part `DEMO-A05` and linked CAD `DEMO-CAD-A05`
- Inspection type: live UI configuration and item-state observation

## Live configuration observed

| ItemType | Versionable | Versioning Discipline | Revisions |
|---|---:|---|---|
| Part | Yes | Automatic | Default |
| CAD | Yes | Manual | Default |

## Behavior observed

After the Part was edited and saved from its released state, the current item became:

- Part: revision `B`, state `Khoi tao`
- Linked CAD: revision `A`, state `Released`

The CAD ItemType's Manual discipline did not automatically advance its revision on a
normal save. The result is a Part-CAD pair whose revisions no longer match.

## Domain conclusion

Automatic/Manual versioning is an Aras ItemType configuration detail. It must not be
used as the product's paired-revision policy. For the controlled PDM workflow:

1. Released Part and CAD revisions are not edited directly.
2. Start New Revision is the only operation that creates the next pair.
3. The authority operation creates the new Part revision and linked CAD revision
   together; the released pair remains unchanged.
4. Released update/lock permissions must independently enforce immutability. Manual
   versioning alone is insufficient.

## Safety and limitations

- No ItemType, lifecycle, permission, or Server Method configuration was changed during
  this inspection.
- This record does not prove the atomicity or concurrency behavior of the deployed
  Start New Revision method.
- No credentials or bearer tokens are stored here.
