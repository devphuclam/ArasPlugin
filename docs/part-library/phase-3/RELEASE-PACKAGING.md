# Release Packaging

## Script

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\release\package-release.ps1 -Version v0.3.0-rc1 -Configuration Release
```

## Output

Default output folder:

```text
artifacts/release/
```

Default zip:

```text
artifacts/release/IdeaCadConnector-v0.3.0-rc1.zip
```

## Zip Structure

```text
IdeaCadConnector-v0.3.0-rc1/
  app/
    IdeaCadConnector.Desktop.exe
    required dll files
    required runtime/config files from Release output
  aras/
    server-methods/
      idea_GetPrimaryIronCadForPart.cs
    README-Aras-Deployment.md
  docs/
    INSTALL.md
    CONFIGURATION.md
    UAT-CHECKLIST.md
    ROLLBACK.md
    RELEASE-NOTES.md
    templates/
      IdeaCadConnector.environment.template.json
  checksums/
    SHA256SUMS.txt
  VERSION.txt
```

## Excluded Content

The package must not include:

- secrets;
- local connection files;
- cached CAD or Vault files;
- source `bin/obj` folders;
- `.vs`;
- `TestResults`;
- personal developer configuration;
- generated test artifacts;
- active `IdeaCadConnector.environment.json` (user-specific config with potential secrets);
- any file named `IdeaCadConnector.environment.json` outside the `docs/templates/` folder.

## Active Config Validation

The packaging script validates that no active `IdeaCadConnector.environment.json` is present in the repository outside the docs template folder before building the release zip. If an active config is found, the script exits with an error to prevent accidental secret inclusion.

## Version File

`VERSION.txt` records:

- version;
- git commit SHA;
- build timestamp in UTC;
- configuration.

## Checksums

`checksums/SHA256SUMS.txt` contains SHA256 checksums for package files. The zip file itself is not included in the internal checksum file because the checksum file is written before compression.
