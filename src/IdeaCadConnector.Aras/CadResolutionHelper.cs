using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    internal static class CadResolutionHelper
    {
        public static bool IsIronCadWithValidNativeFile(string authoringTool, string nativeFile)
        {
            return string.Equals(authoringTool, Core.Cad.CadConstants.IronCadAuthoringTool, System.StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(nativeFile);
        }
    }
}
