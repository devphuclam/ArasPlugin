namespace IdeaCadConnector.Core.Dto
{
    public sealed class CreateCadRequest
    {
        public string PartId { get; set; }

        public string PartNumber { get; set; }

        public string PartClassification { get; set; }
    }
}
