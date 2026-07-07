using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class BrowserLauncher : IBrowserLauncher
    {
        public Task<bool> LaunchUrlAsync(string url, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(url))
                return Task.FromResult(false);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }
    }
}
