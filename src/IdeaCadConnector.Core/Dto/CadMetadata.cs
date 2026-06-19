namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadMetadata
    {
        public string PartId { get; set; }

        public string PartNumber { get; set; }

        public string PartType { get; set; }

        public string Description { get; set; }

        public string Revision { get; set; }

        public string State { get; set; }

        public string CadId { get; set; }

        public string CadNumber { get; set; }

        public string Classification { get; set; }

        public string AuthoringTool { get; set; }

        public string AuthoringToolVersion { get; set; }

        public string Material { get; set; }

        public decimal? Mass { get; set; }

        public string MassUnit { get; set; }
    }
}
