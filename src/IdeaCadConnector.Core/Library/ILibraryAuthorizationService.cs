namespace IdeaCadConnector.Core.Library
{
    public interface ILibraryAuthorizationService
    {
        bool IsLibraryManager { get; }
        bool IsContributorOrHigher { get; }
        bool IsReadOnlyViewer { get; }
        bool CanManageLibraries { get; }
        bool CanUsePartPicker { get; }
    }
}
