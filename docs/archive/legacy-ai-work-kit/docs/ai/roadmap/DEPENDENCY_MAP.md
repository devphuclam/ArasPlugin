# Dependency Map

```text
SEC-00 ✓ — Security baseline (completed)
BASE-00
  └─ BASE-01 ─ BASE-02
       └─ BASE-04 ─ BASE-05
            └─ DOC-01 ─ DOC-02 ─ DOC-03 ─ DOC-04 ─ DOC-05 ─ DOC-06 ─ DOC-07 ─ DOC-08
                                      └─ WSP-01 ─ WSP-02 ─ WSP-03 ─ WSP-04 ─ WSP-05 ─ WSP-06 ─ WSP-07
                                                                   └─ COM-01 ─ COM-02 ─ COM-03 ─ COM-04 ─ COM-05 ─ COM-06 ─ COM-07
                                                                                                          └─ PULL-01..11
                                                                                                                 └─ BR-01..09
                                                                                                                        └─ UI/OPS closeout
```

## Hard gates

- `BASE-04` gates any schema-dependent work.
- `DOC-04` requires verified Document→File linkage.
- Pull requires manifest and diff.
- Remote Branch requires commit parent/head semantics.
- Promote requires branch-specific snapshot and conflict behavior.
