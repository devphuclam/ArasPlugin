# 08 — Definition of Done

A ticket is Done only when all applicable items are true:

- [ ] Ticket scope and non-goals are respected.
- [ ] Code builds in the supported Windows environment.
- [ ] Relevant tests pass.
- [ ] New business logic has meaningful tests.
- [ ] Cancellation is handled.
- [ ] Errors are not converted to false success.
- [ ] Secrets are not logged or committed.
- [ ] Data-loss and rollback behavior is documented.
- [ ] Schema impact is documented.
- [ ] Backward compatibility/migration is addressed.
- [ ] Reviewer findings at BLOCKER/HIGH are resolved.
- [ ] `PROJECT_STATE.md` is updated by the verifier.
- [ ] PR contains exact commands and outputs.
- [ ] Manual Aras/IronCAD validation not performed is explicitly listed.

`Build not available` is not equivalent to `Build passed`.
