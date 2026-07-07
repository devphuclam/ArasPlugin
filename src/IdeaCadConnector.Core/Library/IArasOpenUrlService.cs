namespace IdeaCadConnector.Core.Library
{
    public interface IArasOpenUrlService
    {
        string BuildPartUrl(string partId, string configId);

        string BuildCadUrl(string cadId);

        string BuildLibraryUrl(string libraryId);

        string BuildEntryUrl(string entryId);

        string BuildUserUrl(string userId);
    }
}
