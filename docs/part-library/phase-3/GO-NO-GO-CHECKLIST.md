# Go/No-Go Checklist

## 1. Build/Test

- [ ] Debug build passes (0 warnings, 0 errors)
- [ ] Release build passes (0 warnings, 0 errors)
- [ ] Debug tests pass
- [ ] Release tests pass

## 2. Package Integrity

- [ ] Package script runs successfully
- [ ] Validation script returns exit code 0 on zip
- [ ] Validation script returns exit code 0 on extracted folder
- [ ] VERSION.txt matches expected version
- [ ] SHA256SUMS.txt present
- [ ] App executable present in package

## 3. Aras Readiness

- [ ] `idea_GetPrimaryIronCadForPart` deployed
- [ ] Execute Method permission granted
- [ ] Get permissions on Part, CAD, Part CAD, File granted
- [ ] Get permissions on idea_PartLibrary, idea_PartLibraryEntry granted
- [ ] Aras server URL reachable
- [ ] Target database accessible

## 4. Role Readiness

- [ ] TPTKC (manager) account available
- [ ] TNTKC (reviewer) account available
- [ ] NVTKC (contributor) account available
- [ ] NVLCR (assembly viewer) account available
- [ ] PM (project viewer) account available
- [ ] Khách hàng (customer) account available
- [ ] Role mapping defaults correct

## 5. Machine Readiness

- [ ] Windows 10+ or Windows Server 2016+
- [ ] .NET Framework 4.8 installed
- [ ] Network access to Aras server
- [ ] Default browser configured
- [ ] IronCAD installed (if Open in IronCAD to be tested)
- [ ] Sufficient disk space (~200 MB)

## 6. Security / No Secrets

- [ ] No passwords/tokens in config
- [ ] Active config excluded from package
- [ ] Template has no secret-like keys
- [ ] No credentials committed in repository

## 7. Rollback Readiness

- [ ] Previous release zip available
- [ ] Rollback steps documented
- [ ] User workspaces/vault cache preserved during rollback

## 8. Known Limitations Accepted

- [ ] No MSI/ClickOnce installer — manual zip extraction only
- [ ] Config model not fully wired to all services
- [ ] Open IronCAD depends on local installation
- [ ] CAD download depends on Aras Vault permissions
- [ ] No production deployment executed yet

## 9. Business Owner Sign-Off

- [ ] Confirmed scope is acceptable
- [ ] Confirmed limitations are acceptable
- [ ] Confirmed release candidate is acceptable

## 10. IT/Deployment Owner Sign-Off

- [ ] Aras prerequisites confirmed
- [ ] Machine prerequisites confirmed
- [ ] Network access confirmed
- [ ] Rollback plan confirmed

## Decision

- [ ] **GO** — All mandatory items checked. Release to wider UAT or production.
- [ ] **NO-GO** — One or more mandatory items not met. List blocker below.
- [ ] **GO WITH ACCEPTED LIMITATIONS** — Non-mandatory items not met but accepted.

## Blockers / Notes

```
```
