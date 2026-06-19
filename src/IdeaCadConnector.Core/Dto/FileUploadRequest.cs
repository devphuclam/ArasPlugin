namespace IdeaCadConnector.Core.Dto
{
    public sealed class FileUploadRequest
    {
        public string CheckinTransactionId { get; set; }

        public string FilePath { get; set; }

        public string FileName { get; set; }

        public string ContentType { get; set; }
    }
}
