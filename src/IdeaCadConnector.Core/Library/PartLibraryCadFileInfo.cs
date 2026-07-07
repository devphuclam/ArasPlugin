namespace IdeaCadConnector.Core.Library
{
    public sealed class PartLibraryCadFileInfo
    {
        public string CadId { get; set; }

        public string CadNumber { get; set; }

        public string CadName { get; set; }

        public string CadState { get; set; }

        public string Revision { get; set; }

        public string Generation { get; set; }

        public string FileId { get; set; }

        public string FileName { get; set; }

        public string FileVersion { get; set; }

        public string LockedBy { get; set; }

        public bool HasNative { get; set; }
    }
}
