using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Contracts
{
    public interface IArasCadClient : System.IDisposable
    {
        Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken cancellationToken);

        Task<PartSearchResponse> SearchPartsAsync(
            PartSearchRequest request,
            CancellationToken cancellationToken);

        Task<CreateCadResult> CreateCadAsync(
            CreateCadRequest request,
            CancellationToken cancellationToken);

        Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken cancellationToken);

        Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken cancellationToken);

        Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken cancellationToken);

        Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken cancellationToken);

        Task<CancelCheckoutResult> CancelCheckoutAsync(
            CancelCheckoutRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// Downloads a native file from the Aras vault to a local directory.
        /// </summary>
        Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken cancellationToken);

        Task<CadOperationContext> GetCadOperationContextAsync(
            string cadId,
            CancellationToken cancellationToken = default);

        Task<CadOperationContext> ExecuteCadBusinessActionAsync(
            ExecuteCadBusinessActionRequest request,
            CancellationToken cancellationToken = default);
    }
}
