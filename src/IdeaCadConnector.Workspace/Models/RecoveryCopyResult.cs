namespace IdeaCadConnector.Workspace.Models
{
    public sealed class RecoveryCopyResult
    {
        public bool Succeeded { get; set; }
        public string BackupPath { get; set; }
        public string ErrorMessage { get; set; }
        public string SourceHash { get; set; }
        public string BackupHash { get; set; }
    }
}
