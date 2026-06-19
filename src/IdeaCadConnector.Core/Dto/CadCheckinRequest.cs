using System;

namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadCheckinRequest
    {
        public string CheckinTransactionId { get; set; }

        public string PartId { get; set; }

        public string CadId { get; set; }

        public string LockToken { get; set; }

        public string UploadedFileId { get; set; }

        public string LocalFilePath { get; set; }

        public CadMetadata Metadata { get; set; }

        public string Comment { get; set; }

        public static CadCheckinRequest CreateNew()
        {
            return new CadCheckinRequest
            {
                CheckinTransactionId = Guid.NewGuid().ToString("D"),
                Metadata = new CadMetadata()
            };
        }
    }
}
