using IdeaCadConnector.Core.Cad;

namespace IdeaCadConnector.Core.Library
{
    public sealed class IronCadOpenRequest
    {
        public string FilePath { get; set; }

        public CadOpenMode OpenMode { get; set; }

        public string Source { get; set; }

        public long FileSize { get; set; }

        public bool IsRemoteUrl { get; set; }

        public bool IsTrustedSource { get; set; }
    }
}
