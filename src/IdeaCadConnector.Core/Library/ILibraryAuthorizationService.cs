namespace IdeaCadConnector.Core.Library
{
    public interface ILibraryAuthorizationService
    {
        bool IsLibraryManager { get; }
        bool IsContributorOrHigher { get; }
        bool IsReviewerOrHigher { get; }
        bool IsReadOnlyViewer { get; }
        bool CanManageLibraries { get; }
        bool CanUsePartPicker { get; }
        bool CanMoveEntries { get; }
        bool CanPinRevisions { get; }
    }
}
