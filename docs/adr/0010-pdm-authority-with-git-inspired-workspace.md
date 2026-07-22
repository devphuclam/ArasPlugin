# PDM authority with a Git-inspired workspace

IDEA PDM uses a central PDM Authority for Part/CAD/Document identity, revision, lifecycle, permissions, release, and audit, while borrowing hashing, diff, ChangeSet, snapshot, and recovery ideas for the local Workspace. Git branches, commits, tags, force-push, and binary merge are not the engineer-facing product or revision model because released engineering configurations require controlled, immutable business decisions rather than source-control history.
