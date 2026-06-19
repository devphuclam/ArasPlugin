namespace IdeaCadConnector.Core.Dto
{
    public sealed class PartSearchResult
    {
        public PartSummary Part { get; set; }

        public CadSummary IronCadPartCad { get; set; }
    }
}
