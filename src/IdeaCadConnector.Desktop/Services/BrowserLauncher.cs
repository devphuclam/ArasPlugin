using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class BrowserLauncher : IBrowserLauncher
    {
        private readonly Func<ProcessStartInfo, bool> _startProcess;

        internal BrowserLauncher(Func<ProcessStartInfo, bool> startProcess)
        {
            _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
        }

        public BrowserLauncher()
            : this(startInfo =>
            {
                Process.Start(startInfo);
                return true;
            })
        {
        }

        public Task<bool> LaunchUrlAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return Task.FromResult(false);

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = uri.ToString(),
                    UseShellExecute = true
                };

                cancellationToken.ThrowIfCancellationRequested();
                if (!_startProcess(startInfo))
                    return Task.FromResult(false);

                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }
    }
}
