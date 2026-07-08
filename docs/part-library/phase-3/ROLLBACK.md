# Rollback

## Desktop App Rollback

The Sprint 3.1 release candidate is a zip package. Rollback is file-based:

1. Close `IdeaCadConnector.Desktop.exe`.
2. Move the failed release folder aside.
3. Restore the previous extracted release folder.
4. Launch the previous `app/IdeaCadConnector.Desktop.exe`.
5. Keep user workspaces, Vault cache, and downloaded CAD files untouched unless explicitly instructed by project owner.

## Aras Rollback

Sprint 3.1 does not change live Aras. If the required method is newly deployed for a target UAT database and must be rolled back:

1. Disable or remove `idea_GetPrimaryIronCadForPart`.
2. Confirm Part Library CAD lookup returns the expected missing-method error.
3. Restore the previous app package if needed.

No ItemType, lifecycle, workflow, relationship, CAD, Part, File, or Vault data is changed by Sprint 3.1.

## Checksum Recovery

Use `checksums/SHA256SUMS.txt` to verify extracted package files. If a checksum mismatch is found, discard the extraction and unzip a fresh copy.
