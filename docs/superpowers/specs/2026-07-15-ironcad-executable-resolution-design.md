# IronCAD Executable Resolution Design

## Problem

Checkout downloads and locks the CAD successfully, but opening the local file fails when `local.ironCadExecutablePath` is blank. The workstation has IronCAD 2025 installed at `C:\Program Files\IronCAD\2025\bin\IRONCAD.exe`, so requiring a manually configured path is unnecessary and harms the default checkout experience.

## Scope

Add one executable resolver used by the desktop IronCAD adapter and open service. Do not change Aras locking, Vault download/upload, check-in, cancel-checkout, lifecycle, or authorization behavior.

## Resolution Order

1. Use the explicitly configured executable path when it exists.
2. Use the executable path of a running `IRONCAD` process.
3. Read the Windows App Paths and IronCAD installation registry entries.
4. Search versioned `IronCAD/<version>/bin/IRONCAD.exe` directories under 64-bit and 32-bit Program Files, preferring the highest version.

Only an existing local file named `IRONCAD.exe` is accepted. Invalid configured paths fall through to discovery instead of blocking checkout.

## Integration

The resolver returns a path or no result. `IronCadExternalAdapter` uses the resolved path when starting IronCAD. `IronCadOpenService.IsIronCadAvailable` uses the same resolver so availability and launch behavior cannot disagree. Existing dependency-injection seams remain usable by tests.

## Errors and Safety

Registry, process, and filesystem discovery failures are non-fatal and move to the next source. If no executable is found, return the existing user-facing open failure without changing the server checkout state; Cancel Checkout remains available.

## Verification

Tests cover configured-path precedence, invalid-config fallback, running-process/registry/install-directory discovery through injected candidates, highest-version selection, no-install behavior, and adapter/open-service consistency. The full test suite and desktop build must pass.
