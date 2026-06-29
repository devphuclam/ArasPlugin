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
        public int StructureNodeCount { get; set; }
        public int CadFileCount { get; set; }
        public int DocumentFileCount { get; set; }
    }

    public sealed class WorkspaceCommitHistory
    {
        public List<WorkspaceCommit> Commits { get; set; } = new List<WorkspaceCommit>();
    }
}
