# IOM.dll

`IOM.dll` (Aras Innovator Object Model) is the official .NET SDK for Aras Innovator
integrations. It is **not available on NuGet** but may be redistributed as part of
custom solutions and add-ins.

## Build Setup

Place `IOM.dll` in this directory so the `.csproj` reference resolves:

```
lib/IOM.dll
```

### Where to get IOM.dll

| Source | Path |
|---|---|
| Aras Server installation | `C:\Program Files\Aras\Innovator\Client\IOM.dll` |
| Aras Client installation | `C:\Program Files\Aras\Innovator Client\IOM.dll` |
| Existing repo copy | Ask a teammate who already has it |

### For CI / new dev machines

Copy from one of the above sources into `lib/IOM.dll`. The file is ~200 KB and
should be committed to git so every developer and build agent has it.

## Version Compatibility

This add-in targets **Aras Innovator 14.35.0**. If your Aras server is a different
version, replace IOM.dll with the matching version from your server installation.
