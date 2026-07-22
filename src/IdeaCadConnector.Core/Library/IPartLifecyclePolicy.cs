namespace IdeaCadConnector.Core.Library
{
    public interface IPartLifecyclePolicy
    {
        bool CanRelease(string state);
        bool IsReleased(string state);
    }
}
