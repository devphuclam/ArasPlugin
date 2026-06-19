namespace IdeaCadConnector.Core.Dto
{
    public sealed class ArasLoginRequest
    {
        public string ServerUrl { get; set; }

        public string Database { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }
    }
}
