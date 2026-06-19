using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Contracts
{
    public interface ICadApplicationAdapter
    {
        string AuthoringTool { get; }

        string AuthoringToolVersion { get; }

        CadDocumentInfo GetActiveDocumentInfo();

        CadMetadata ReadMetadata();

        void WriteMetadata(CadMetadata metadata);

        Task OpenDocumentAsync(string filePath, CadOpenMode openMode, CancellationToken cancellationToken);
    }
}
