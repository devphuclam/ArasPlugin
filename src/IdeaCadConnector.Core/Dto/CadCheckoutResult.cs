namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadCheckoutResult
    {
        public string CheckoutSessionId { get; set; }

        public string LockToken { get; set; }

        public PartSummary Part { get; set; }

        public CadSummary Cad { get; set; }

        public CadMetadata Metadata { get; set; }

        public bool IsReadOnly { get; set; }
    }
}
