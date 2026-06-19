namespace IdeaCadConnector.Core.Dto
{
    public sealed class CancelCheckoutRequest
    {
        public string CadId { get; set; }

        public string LockToken { get; set; }
    }
}
