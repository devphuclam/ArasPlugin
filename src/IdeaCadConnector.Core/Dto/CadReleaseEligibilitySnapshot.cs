namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadReleaseEligibilitySnapshot
    {
        public string CadId { get; set; }
        public string PartId { get; set; }
        public string CadState { get; set; }
        public string PartState { get; set; }
    }
}
