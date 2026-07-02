using System;
using System.Collections.Generic;
using System.Linq;

namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceCommit
    {
        public string CommitId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Branch { get; set; }
        public string Message { get; set; }
        public string ProjectFolder { get; set; }
        public string RepositoryCode { get; set; }
        // TODO(PERF-COMMIT-FILES): Replace with List<PdmCommitFileEntry> when
        // per-file detail is introduced. These counts are summary-only and
        // cannot be reverse-mapped to individual files.
        public int StructureNodeCount { get; set; }
        public int CadFileCount { get; set; }
        public int DocumentFileCount { get; set; }
        public int LibraryReferenceCount { get; set; }
        // TODO(PERF-CONTENT-HASH): Replace SnapshotSignature with SHA256-based
        // PdmContentHasher when Phase 1 index is introduced. Current signature
        // compares file identity only (paths + keys), not file content.
        public string SnapshotSignature { get; set; }
        // TODO(PERF-COMMIT-GRAPH): Add nullable ParentCommitId field when
        // commit graph is needed. Currently all commits are flat.
        // TODO(PERF-COMMIT-FILES): Add List<PdmCommitFileEntry> Files when
        // per-file model is introduced.
        // TODO(PERF-COMMIT-AUTHOR): Add Author field when server-backed
        // commits are introduced. Currently local-only, no author tracking.
    }

    public sealed class WorkspaceCommitHistory
    {
        public List<WorkspaceCommit> Commits { get; set; } = new List<WorkspaceCommit>();
    }

    public sealed class WorkspaceBranch
    {
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class WorkspaceBranchRegistry
    {
        public List<WorkspaceBranch> Branches { get; set; } = new List<WorkspaceBranch>();
    }
}
