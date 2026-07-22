# Permission-Respect Evidence

## Scope

- Feature: `003-controlled-cad-design-release`
- Date: 2026-07-21
- Environment: Aras Innovator Community Edition live instance used for the controlled fixture
- User: `admin`
- Fixture: Part `DEMO-A05` / CAD `DEMO-CAD-A05`, both revision `A`, both `Released`

## Observed result

From the released CAD revision, the administrator opened `More -> Create New Revision`.
Aras rejected the operation with the server message:

> You must be a member of the Owner identity to perform this action.

No revision was created and no client-side bypass was attempted. The original Part/CAD
pair remained at revision `A` and `Released`.

## Interpretation

This is direct evidence that the live authority permission is enforced and that an
administrator session is not automatically treated as the revision owner. The client
must preserve and surface this authority error rather than replacing it with a local
success result.

## Limitations

- This replay covered the Aras UI `Create New Revision` path, not every desktop-client
  transport call path.
- The controlled fixture was not modified to add `admin` to the `Owner` identity.
- Start-New-Revision performance and concurrent-revision behavior remain unverified
  because the authority rejected the prerequisite operation.
- No token or other credential is stored in this evidence record.
