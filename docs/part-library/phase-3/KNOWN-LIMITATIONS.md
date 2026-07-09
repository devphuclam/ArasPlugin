# Known Limitations and Accepted Risks

These are the accepted limitations for v0.3.0-rc1. Phase 3 is `COMPLETE` with these limitations documented and accepted.

## Installer

- No MSI, ClickOnce, or auto-update system.
- Manual zip extraction required.
- User must manually create shortcuts.

## Environment Configuration

- Active config file is optional and non-secret only.
- Config model is not fully wired to all runtime services.
- See [ENVIRONMENT-CONFIGURATION.md](ENVIRONMENT-CONFIGURATION.md) for wiring status.

## Open in IronCAD

- Requires local IronCAD installation.
- Requires IronCAD file association or configured executable path.
- If IronCAD is not installed, Open in IronCAD is unavailable.

## CAD Download

- Depends on Aras Vault/File permissions.
- Depends on network access to Vault server.
- If user lacks Vault read permission, download fails.

## Customer Visibility

- Depends on Aras permissions on Part, CAD, Part CAD, File, and Library ItemTypes.
- If permissions are insufficient, the customer sees empty or error states.

## Library Restore

- Library restore flows are not in scope.

## Production Rollout

- Production deployment has not been executed.
- Phase 3 closeout does not include production rollout execution.
- Production rollout is outside this closeout unless the owner decides separately.

## Role Mapping

- Role matching is username/config-based.
- Future hardening could use Aras Identity membership for more robust mapping.
