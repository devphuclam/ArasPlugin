using System;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class ArasOpenUrlServiceTests
    {
        private static readonly Uri TestBaseUri = new Uri("http://innovator-test/InnovatorServer/");
        private const string TestDatabase = "InnovatorSolutions";

        [Fact]
        public void BuildPartUrl_WithConfigId_UsesConfigId()
        {
            var service = CreateService();
            var url = service.BuildPartUrl("part-1", "cfg-1");

            Assert.Contains("cfg-1", url);
            Assert.Contains("type=Part", url);
            Assert.Contains("db=InnovatorSolutions", url);
            Assert.DoesNotContain("part-1", url);
        }

        [Fact]
        public void BuildPartUrl_WithoutConfigId_UsesPartId()
        {
            var service = CreateService();
            var url = service.BuildPartUrl("part-1", null);

            Assert.Contains("part-1", url);
            Assert.Contains("type=Part", url);
        }

        [Fact]
        public void BuildPartUrl_EmptyIds_ReturnsNull()
        {
            var service = CreateService();
            Assert.Null(service.BuildPartUrl(null, null));
            Assert.Null(service.BuildPartUrl("", null));
        }

        [Fact]
        public void BuildCadUrl_IncludesCadId()
        {
            var service = CreateService();
            var url = service.BuildCadUrl("cad-123");

            Assert.Contains("cad-123", url);
            Assert.Contains("type=CAD", url);
        }

        [Fact]
        public void BuildCadUrl_NullId_ReturnsNull()
        {
            var service = CreateService();
            Assert.Null(service.BuildCadUrl(null));
        }

        [Fact]
        public void BuildLibraryUrl_IncludesLibraryId()
        {
            var service = CreateService();
            var url = service.BuildLibraryUrl("lib-456");

            Assert.Contains("lib-456", url);
            Assert.Contains("type=idea_PartLibrary", url);
        }

        [Fact]
        public void BuildLibraryUrl_NullId_ReturnsNull()
        {
            var service = CreateService();
            Assert.Null(service.BuildLibraryUrl(null));
        }

        [Fact]
        public void BuildEntryUrl_IncludesEntryId()
        {
            var service = CreateService();
            var url = service.BuildEntryUrl("entry-789");

            Assert.Contains("entry-789", url);
            Assert.Contains("type=idea_PartLibraryEntry", url);
        }

        [Fact]
        public void BuildEntryUrl_NullId_ReturnsNull()
        {
            var service = CreateService();
            Assert.Null(service.BuildEntryUrl(null));
        }

        [Fact]
        public void AllUrls_StartWithBaseUri()
        {
            var service = CreateService();

            Assert.StartsWith(TestBaseUri.ToString().TrimEnd('/'), service.BuildPartUrl("p1", "c1"));
            Assert.StartsWith(TestBaseUri.ToString().TrimEnd('/'), service.BuildCadUrl("c1"));
            Assert.StartsWith(TestBaseUri.ToString().TrimEnd('/'), service.BuildLibraryUrl("l1"));
            Assert.StartsWith(TestBaseUri.ToString().TrimEnd('/'), service.BuildEntryUrl("e1"));
        }

        [Fact]
        public void AllUrls_IncludeResourceAspx()
        {
            var service = CreateService();

            Assert.Contains("/resource.aspx", service.BuildPartUrl("p1", "c1"));
            Assert.Contains("/resource.aspx", service.BuildCadUrl("c1"));
            Assert.Contains("/resource.aspx", service.BuildLibraryUrl("l1"));
            Assert.Contains("/resource.aspx", service.BuildEntryUrl("e1"));
        }

        [Fact]
        public void AllUrls_IncludeDatabase()
        {
            var service = CreateService();

            Assert.Contains("db=InnovatorSolutions", service.BuildPartUrl("p1", "c1"));
            Assert.Contains("db=InnovatorSolutions", service.BuildCadUrl("c1"));
            Assert.Contains("db=InnovatorSolutions", service.BuildLibraryUrl("l1"));
            Assert.Contains("db=InnovatorSolutions", service.BuildEntryUrl("e1"));
        }

        [Fact]
        public void SpecialCharacters_AreUrlEncoded()
        {
            var service = CreateService();
            var url = service.BuildPartUrl("part id", "cfg id");

            Assert.Contains("cfg%20id", url);
            Assert.DoesNotContain(" ", url);
        }

        [Fact]
        public void Constructor_NullBaseUri_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ArasOpenUrlService(null, "db"));
        }

        [Fact]
        public void Constructor_NullDatabase_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ArasOpenUrlService(TestBaseUri, null));
        }

        [Fact]
        public async Task BuildUrlAsync_NullRequest_ReturnsValidationFailed()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task BuildUrlAsync_MissingItemType_ReturnsValidationFailed()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemId = "id-1" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task BuildUrlAsync_MissingIds_ReturnsValidationFailed()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemType = "Part" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task BuildUrlAsync_UnapprovedItemType_ReturnsValidationFailed()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemType = "User", ItemId = "user-1" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
            Assert.Contains("User", result.ErrorMessage);
        }

        [Fact]
        public async Task BuildUrlAsync_PartType_Success()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemType = "Part", ItemId = "part-1" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("part-1", result.Url);
            Assert.Contains("type=Part", result.Url);
        }

        [Fact]
        public async Task BuildUrlAsync_CadType_Success()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemType = "CAD", ItemId = "cad-1" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("cad-1", result.Url);
            Assert.Contains("type=CAD", result.Url);
        }

        [Fact]
        public async Task BuildUrlAsync_LibraryType_Success()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemType = "idea_PartLibrary", ItemId = "lib-1" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("lib-1", result.Url);
            Assert.Contains("type=idea_PartLibrary", result.Url);
        }

        [Fact]
        public async Task BuildUrlAsync_EntryType_Success()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemType = "idea_PartLibraryEntry", ItemId = "entry-1" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("entry-1", result.Url);
            Assert.Contains("type=idea_PartLibraryEntry", result.Url);
        }

        [Fact]
        public async Task BuildUrlAsync_UsesConfigIdWhenProvided()
        {
            var service = CreateService();
            var result = await service.BuildUrlAsync(
                new ArasOpenUrlRequest { ItemType = "Part", ItemId = "part-1", ConfigId = "cfg-1" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("cfg-1", result.Url);
            Assert.DoesNotContain("part-1", result.Url);
        }

        [Fact]
        public async Task BuildUrlAsync_Cancelled_Throws()
        {
            var service = CreateService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                service.BuildUrlAsync(
                    new ArasOpenUrlRequest { ItemType = "Part", ItemId = "p1" },
                    cts.Token));
        }

        private static ArasOpenUrlService CreateService()
        {
            return new ArasOpenUrlService(TestBaseUri, TestDatabase);
        }
    }
}
