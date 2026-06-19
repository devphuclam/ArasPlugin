namespace IdeaCadConnector.Core.Dto
{
    public sealed class FileUploadResult
    {
        public string UploadedFileId { get; set; }

        public string FileName { get; set; }

        public long SizeBytes { get; set; }

        public string Checksum { get; set; }
    }
}
