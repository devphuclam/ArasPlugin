namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadCheckinResult
    {
        public bool Success { get; set; }

        public PartSummary Part { get; set; }

        public CadSummary Cad { get; set; }

        public string Message { get; set; }
    }
}
