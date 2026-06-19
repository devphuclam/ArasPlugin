namespace IdeaCadConnector.Core.Dto
{
    public sealed class PartSearchRequest
    {
        public string Keyword { get; set; }

        public int MaxResults { get; set; }

        public int Skip { get; set; }
    }
}
