# 07 — Known Limitations

- Pull command is currently not a real sync implementation.
- Clone downloads CAD native files but Document paths may be zero-byte placeholders.
- Document push currently focuses on metadata/relationships; physical attachment schema must be verified.
- Non-main branches are local/staging-oriented rather than complete remote snapshots.
- PDM Commit support is best-effort when server schema is unavailable.
- Commit file change type and Vault linkage require completion.
- Some sidebar buttons/screens are placeholders or partially wired.
- OCR project may depend on machine-specific Windows SDK references.
- Build requires Windows/.NET Framework 4.8/WPF tooling and external CAD/Aras dependencies.
- Live server methods and permissions cannot be assumed deployed from source presence alone.
