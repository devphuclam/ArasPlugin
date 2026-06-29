using System;

namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceManifest
    {
        public string ProjectFolder { get; set; }
        public string PartId { get; set; }
        public string PartNumber { get; set; }
        public string CadId { get; set; }
        public string CadNumber { get; set; }
        public string NativeFileId { get; set; }
        public string LocalFilePath { get; set; }
        public string LockToken { get; set; }
        public string LockedBy { get; set; }
        public DateTime CheckedOutAt { get; set; }
        public DateTime? LastKnownModifiedOn { get; set; }
        public string Branch { get; set; }
        public string LastKnownRevision { get; set; }
        public int LastKnownGeneration { get; set; }
    }
}
