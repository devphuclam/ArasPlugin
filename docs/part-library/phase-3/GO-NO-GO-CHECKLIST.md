# Go/No-Go Checklist

## 1. Build/Test

- [x] Debug build passes (0 warnings, 0 errors)
- [x] Release build passes (0 warnings, 0 errors)
- [x] Debug tests pass (419/419)
- [x] Release tests pass (419/419)

## 2. Package Integrity

- [x] Package script runs successfully
- [x] Validation script returns exit code 0 on zip
- [x] Validation script returns exit code 0 on extracted folder
- [x] VERSION.txt matches expected version
- [x] SHA256SUMS.txt present
- [x] App executable present in package

## 3. Aras Readiness

- [x] `idea_GetPrimaryIronCadForPart` deployed
- [x] Execute Method permission granted
- [x] Get permissions on Part, CAD, Part CAD, File granted
- [x] Get permissions on idea_PartLibrary, idea_PartLibraryEntry granted
- [x] Aras server URL reachable
- [x] Target database accessible

## 4. Role Readiness

- [x] ExampleManager (manager) account available
- [x] ExampleReviewer (reviewer) account available
- [x] ExampleContributor (contributor) account available
- [x] ExampleAssemblyViewer (assembly viewer) account available
- [x] ExampleProjectViewer (project viewer) account available
- [x] Khách hàng (customer) account available
- [x] Role mapping defaults correct

## 5. Machine Readiness

- [x] Windows 10+ or Windows Server 2016+
- [x] .NET Framework 4.8 installed
- [x] Network access to Aras server
- [x] Default browser configured
- [x] IronCAD installed (if Open in IronCAD to be tested)
- [x] Sufficient disk space (~200 MB)

## 6. Security / No Secrets

- [x] No passwords/tokens in config
- [x] Active config excluded from package
- [x] Template has no secret-like keys
- [x] No credentials committed in repository

## 7. Rollback Readiness

- [x] Previous release zip available
- [x] Rollback steps documented
- [x] User workspaces/vault cache preserved during rollback

## 8. Known Limitations Accepted

- [x] No MSI/ClickOnce installer — manual zip extraction only
- [x] Config model not fully wired to all services
- [x] Open IronCAD depends on local installation
- [x] CAD download depends on Aras Vault permissions
- [x] No production deployment executed yet

## 9. Business Owner Sign-Off

- [x] Confirmed scope is acceptable
- [x] Confirmed limitations are acceptable
- [x] Confirmed release candidate is acceptable

## 10. IT/Deployment Owner Sign-Off

- [x] Aras prerequisites confirmed
- [x] Machine prerequisites confirmed
- [x] Network access confirmed
- [x] Rollback plan confirmed

## Decision

- [x] **GO** — All mandatory items checked. Release to wider UAT or production.
- [ ] **NO-GO** — One or more mandatory items not met. List blocker below.
- [ ] **GO WITH ACCEPTED LIMITATIONS** — Non-mandatory items not met but accepted.

## Blockers / Notes

```
```
