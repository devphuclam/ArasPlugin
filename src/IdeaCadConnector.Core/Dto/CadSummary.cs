namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadSummary
    {
        public string Id { get; set; }

        public string CadNumber { get; set; }

        public string Classification { get; set; }

        public string Revision { get; set; }

        public string State { get; set; }

        public int Generation { get; set; }

        public string NativeFileId { get; set; }

        public bool HasNativeFile { get; set; }

        public bool IsLocked { get; set; }

        public string LockedBy { get; set; }
    }
}
