namespace IdeaCadConnector.Core.Dto.Library
{
    public sealed class LibraryEntryCadDetails
    {
        public string PrimaryCadId { get; set; }

        public string PrimaryCadNumber { get; set; }

        public string PrimaryCadName { get; set; }

        public string PrimaryCadState { get; set; }

        public string FileId { get; set; }

        public string FileName { get; set; }

        public string FileVersion { get; set; }

        public string LockedBy { get; set; }

        public bool HasNative { get; set; }

        public string CadStatus { get; set; }
    }
}
