namespace IdeaCadConnector.Core.Dto
{
    public sealed class ArasLoginResult
    {
        public string SessionToken { get; set; }

        public string TokenType { get; set; }

        public string UserId { get; set; }

        public string UserName { get; set; }

        public string DisplayName { get; set; }

        public string Database { get; set; }
    }
}
