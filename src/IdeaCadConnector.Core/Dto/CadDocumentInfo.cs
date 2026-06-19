namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadDocumentInfo
    {
        public string FullPath { get; set; }

        public string FileName { get; set; }

        public string Extension { get; set; }

        public bool IsDirty { get; set; }
    }
}
