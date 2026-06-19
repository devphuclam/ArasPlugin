namespace IdeaCadConnector.Core.Dto
{
    public sealed class CreateCadResult
    {
        public PartSummary Part { get; set; }

        public CadSummary Cad { get; set; }

        public CadCheckoutResult Checkout { get; set; }
    }
}
