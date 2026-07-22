using System;

namespace IdeaCadConnector.Workspace.Models
{
    public sealed class RecoveryCopyRecord
    {
        public Guid RecoveryId { get; set; }
        public string CadId { get; set; }
        public string SourcePath { get; set; }
        public string BackupPath { get; set; }
        public string SourceHash { get; set; }
        public string BackupHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime RetentionUntil { get; set; }
    }
}
