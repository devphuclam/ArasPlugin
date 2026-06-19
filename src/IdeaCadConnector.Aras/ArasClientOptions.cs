using System;

namespace IdeaCadConnector.Aras
{
    public sealed class ArasClientOptions
    {
        public Uri BaseUri { get; set; } = new Uri("http://172.16.10.227/InnovatorServer/", UriKind.Absolute);

        public string Database { get; set; } = "InnovatorSolutions";

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        public string VaultId { get; set; } = "67BBB9204FE84A8981ED8313049BA06C";

        public string OAuthClientId { get; set; } = "IOMApp";

        public string OAuthScope { get; set; } = "Innovator";

        public string IronCadExecutablePath { get; set; } = @"C:\Program Files\IronCAD\2025\bin\IRONCAD.exe";

        public int DefaultMaxSearchResults { get; set; } = 20;
    }
}
