using IdeaCadConnector.Core.Configuration;

namespace IdeaCadConnector.Core.Contracts
{
    public interface IPdmRoleProvider
    {
        PdmUserRole GetRole(string userName);
    }
}
