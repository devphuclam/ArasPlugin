# AI Ticket Backlog

| ID | Epic | Title | Dependencies | Risk | Initial status |
|---|---|---|---|---|---|---|
| [SEC-00](tickets/SEC-00-externalize-aras-environment-config.md) | Security | Externalize Aras environment config from source control | None | High | Ready |
| [BASE-00](tickets/BASE-00-establish-clean-backed-up-baseline.md) | Baseline | Establish clean backed-up baseline | None | High | Ready |
| [BASE-01](tickets/BASE-01-build-baseline.md) | Baseline | Build baseline | BASE-00 | High | Blocked |
| [BASE-02](tickets/BASE-02-test-baseline.md) | Baseline | Test baseline | BASE-01 | Medium | Blocked |
| [BASE-03](tickets/BASE-03-install-ai-governance-workflow.md) | Baseline | Install AI governance workflow | BASE-00 | Low | Blocked |
| [BASE-04](tickets/BASE-04-verify-aras-schema-map.md) | Baseline | Verify Aras schema map | BASE-01 | Critical | Blocked |
| [BASE-05](tickets/BASE-05-inventory-deployed-server-methods.md) | Baseline | Inventory deployed server methods | BASE-04 | High | Blocked |
| [DOC-01](tickets/DOC-01-extend-document-push-contract.md) | Document Vault | Extend Document push contract | BASE-04 | Medium | Blocked |
| [DOC-02](tickets/DOC-02-populate-document-path-and-fingerprint.md) | Document Vault | Populate Document path and fingerprint | DOC-01 | Medium | Blocked |
| [DOC-03](tickets/DOC-03-upload-document-file-to-vault.md) | Document Vault | Upload Document file to Vault | DOC-02 | High | Blocked |
| [DOC-04](tickets/DOC-04-attach-physical-file-to-document.md) | Document Vault | Attach physical File to Document | DOC-03 | Critical | Blocked |
| [DOC-05](tickets/DOC-05-implement-document-content-version-policy.md) | Document Vault | Implement Document content version policy | DOC-04 | High | Blocked |
| [DOC-06](tickets/DOC-06-download-real-document-files-during-clone.md) | Document Vault | Download real Document files during Clone | DOC-05 | High | Blocked |
| [DOC-07](tickets/DOC-07-retain-placeholder-compatibility-mode.md) | Document Vault | Retain placeholder compatibility mode | DOC-06 | Medium | Blocked |
| [DOC-08](tickets/DOC-08-document-vault-integration-suite.md) | Document Vault | Document Vault integration suite | DOC-07 | High | Blocked |
| [WSP-01](tickets/WSP-01-design-manifest-v2-model.md) | Workspace State | Design manifest v2 model | DOC-04 | Medium | Blocked |
| [WSP-02](tickets/WSP-02-migrate-legacy-manifest-to-v2.md) | Workspace State | Migrate legacy manifest to v2 | WSP-01 | High | Blocked |
| [WSP-03](tickets/WSP-03-implement-workspace-file-scanner.md) | Workspace State | Implement workspace file scanner | WSP-02 | Medium | Blocked |
| [WSP-04](tickets/WSP-04-implement-reusable-sha256-service.md) | Workspace State | Implement reusable SHA256 service | WSP-03 | Medium | Blocked |
| [WSP-05](tickets/WSP-05-implement-file-diff-engine.md) | Workspace State | Implement file Diff Engine | WSP-04 | High | Blocked |
| [WSP-06](tickets/WSP-06-implement-part-and-bom-diff.md) | Workspace State | Implement Part and BOM diff | WSP-05 | High | Blocked |
| [WSP-07](tickets/WSP-07-write-manifest-atomically.md) | Workspace State | Write manifest atomically | WSP-06 | Critical | Blocked |
| [COM-01](tickets/COM-01-record-authenticated-commit-author.md) | Commit History | Record authenticated commit author | WSP-07 | Medium | Blocked |
| [COM-02](tickets/COM-02-add-parent-commit-semantics.md) | Commit History | Add parent commit semantics | COM-01 | High | Blocked |
| [COM-03](tickets/COM-03-create-per-file-commit-entries.md) | Commit History | Create per-file commit entries | COM-02 | High | Blocked |
| [COM-04](tickets/COM-04-use-correct-commit-change-types.md) | Commit History | Use correct commit change types | COM-03,WSP-05 | High | Blocked |
| [COM-05](tickets/COM-05-persist-vault-file-id-in-commit-file.md) | Commit History | Persist Vault File ID in commit file | COM-04,DOC-04 | High | Blocked |
| [COM-06](tickets/COM-06-query-and-display-commit-history.md) | Commit History | Query and display commit history | COM-05 | Medium | Blocked |
| [COM-07](tickets/COM-07-commit-integration-suite.md) | Commit History | Commit integration suite | COM-06 | High | Blocked |
| [PULL-01](tickets/PULL-01-define-pull-and-remote-snapshot-contracts.md) | Pull/Sync | Define Pull and remote snapshot contracts | WSP-07,COM-05 | Medium | Blocked |
| [PULL-02](tickets/PULL-02-fetch-remote-snapshot.md) | Pull/Sync | Fetch remote snapshot | PULL-01 | High | Blocked |
| [PULL-03](tickets/PULL-03-implement-three-way-comparison.md) | Pull/Sync | Implement three-way comparison | PULL-02,WSP-05,WSP-06 | Critical | Blocked |
| [PULL-04](tickets/PULL-04-build-pull-plan.md) | Pull/Sync | Build Pull plan | PULL-03 | High | Blocked |
| [PULL-05](tickets/PULL-05-add-pull-preview-ui.md) | Pull/Sync | Add Pull Preview UI | PULL-04 | Medium | Blocked |
| [PULL-06](tickets/PULL-06-download-remote-files-to-temp.md) | Pull/Sync | Download remote files to temp | PULL-04 | High | Blocked |
| [PULL-07](tickets/PULL-07-implement-backup-apply-and-rollback.md) | Pull/Sync | Implement backup, apply and rollback | PULL-06,WSP-07 | Critical | Blocked |
| [PULL-08](tickets/PULL-08-define-conflict-model-and-policies.md) | Pull/Sync | Define conflict model and policies | PULL-03 | High | Blocked |
| [PULL-09](tickets/PULL-09-add-conflict-resolution-ui.md) | Pull/Sync | Add conflict resolution UI | PULL-08,PULL-05 | High | Blocked |
| [PULL-10](tickets/PULL-10-protect-checkout-and-open-files.md) | Pull/Sync | Protect checkout and open files | PULL-07 | Critical | Blocked |
| [PULL-11](tickets/PULL-11-pull-integration-and-failure-suite.md) | Pull/Sync | Pull integration and failure suite | PULL-09,PULL-10 | Critical | Blocked |
| [BR-01](tickets/BR-01-define-and-deploy-pdm-branch-schema.md) | Remote Branch | Define and deploy PDM Branch schema | COM-07,PULL-11,BASE-04 | Critical | Blocked |
| [BR-02](tickets/BR-02-implement-remote-branch-repository-service.md) | Remote Branch | Implement remote branch repository service | BR-01 | High | Blocked |
| [BR-03](tickets/BR-03-manage-branch-head-atomically.md) | Remote Branch | Manage branch head atomically | BR-02,COM-02 | Critical | Blocked |
| [BR-04](tickets/BR-04-clone-branch-specific-snapshot.md) | Remote Branch | Clone branch-specific snapshot | BR-03,COM-05 | High | Blocked |
| [BR-05](tickets/BR-05-pull-branch-specific-snapshot.md) | Remote Branch | Pull branch-specific snapshot | BR-04,PULL-11 | High | Blocked |
| [BR-06](tickets/BR-06-push-branch-specific-commit.md) | Remote Branch | Push branch-specific commit | BR-03,COM-07 | Critical | Blocked |
| [BR-07](tickets/BR-07-switch-branch-safely.md) | Remote Branch | Switch branch safely | BR-04,BR-05 | Critical | Blocked |
| [BR-08](tickets/BR-08-promote-branch-to-main.md) | Remote Branch | Promote branch to main | BR-06,BR-07 | Critical | Blocked |
| [BR-09](tickets/BR-09-branch-permission-and-concurrency-suite.md) | Remote Branch | Branch permission and concurrency suite | BR-08 | High | Blocked |
| [UI-01](tickets/UI-01-wire-projects-navigation.md) | Application Completion | Wire Projects navigation | PULL-05 | Low | Blocked |
| [UI-02](tickets/UI-02-implement-recent-screen.md) | Application Completion | Implement Recent screen | COM-06 | Low | Blocked |
| [UI-03](tickets/UI-03-complete-favorites-screen.md) | Application Completion | Complete Favorites screen | UI-01 | Medium | Blocked |
| [UI-04](tickets/UI-04-implement-operational-reports.md) | Application Completion | Implement operational Reports | PULL-11,COM-06 | Medium | Blocked |
| [UI-05](tickets/UI-05-implement-settings-screen.md) | Application Completion | Implement Settings screen | BASE-04 | High | Blocked |
| [UI-06](tickets/UI-06-implement-about-diagnostics-screen.md) | Application Completion | Implement About/Diagnostics screen | OPS-01 | Low | Blocked |
| [UI-07](tickets/UI-07-hide-or-flag-unavailable-features.md) | Application Completion | Hide or flag unavailable features | UI-01 | Low | Blocked |
| [OPS-01](tickets/OPS-01-add-structured-operation-logging.md) | Operations | Add structured operation logging | BASE-01 | Medium | Blocked |
| [OPS-02](tickets/OPS-02-add-operation-ids-and-progress.md) | Operations | Add operation IDs and progress | OPS-01 | Medium | Blocked |
| [OPS-03](tickets/OPS-03-add-bounded-retry-policy.md) | Operations | Add bounded retry policy | OPS-02 | High | Blocked |
| [OPS-04](tickets/OPS-04-enforce-secret-redaction.md) | Operations | Enforce secret redaction | OPS-01 | High | Blocked |
| [OPS-05](tickets/OPS-05-build-aras-deployment-package.md) | Operations | Build Aras deployment package | BASE-05,BR-01 | Critical | Blocked |
| [OPS-06](tickets/OPS-06-add-schema-version-compatibility-check.md) | Operations | Add schema version compatibility check | OPS-05 | High | Blocked |
| [OPS-07](tickets/OPS-07-create-repeatable-integration-environment-guide.md) | Operations | Create repeatable integration environment guide | OPS-05 | Medium | Blocked |
| [OPS-08](tickets/OPS-08-execute-uat-checklist.md) | Operations | Execute UAT checklist | UI-07,BR-09,OPS-07 | High | Blocked |
| [OPS-09](tickets/OPS-09-prepare-production-release-documentation.md) | Operations | Prepare production release documentation | OPS-08 | High | Blocked |
