using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class BrowserLauncherTests
    {
        [Fact]
        public async Task LaunchUrlAsync_ValidHttpUrl_UsesShellExecute()
        {
            ProcessStartInfo captured = null;
            var launcher = new BrowserLauncher(startInfo =>
            {
                captured = startInfo;
                return true;
            });

            var result = await launcher.LaunchUrlAsync("http://innovator-test/InnovatorServer/", CancellationToken.None);

            Assert.True(result);
            Assert.NotNull(captured);
            Assert.Equal("http://innovator-test/InnovatorServer/", captured.FileName);
            Assert.True(captured.UseShellExecute);
        }

        [Fact]
        public async Task LaunchUrlAsync_ValidHttpsUrl_ReturnsTrue()
        {
            var launcher = new BrowserLauncher(_ => true);

            var result = await launcher.LaunchUrlAsync("https://innovator-test/InnovatorServer/", CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public async Task LaunchUrlAsync_InvalidUrl_ReturnsFalse()
        {
            var launcher = new BrowserLauncher(_ => true);

            Assert.False(await launcher.LaunchUrlAsync("not-a-url", CancellationToken.None));
            Assert.False(await launcher.LaunchUrlAsync("file:///C:/temp/test.txt", CancellationToken.None));
        }

        [Fact]
        public async Task LaunchUrlAsync_CanceledBeforeLaunch_ThrowsOperationCanceled()
        {
            var launcher = new BrowserLauncher(_ => true);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => launcher.LaunchUrlAsync("https://innovator-test/InnovatorServer/", cts.Token));
        }
    }
}
