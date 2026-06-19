using System;
using IdeaCadConnector.Core.Cad;

namespace IdeaCadConnector.Core.Validation
{
    // Naming for managed CAD records and native files is enforced by Aras
    // server-side methods (e.g. idea_EnsurePrimaryIronCadPartCad). The client
    // does not invent CAD numbers; it consumes whatever the server returns.
    //
    // The single helper kept here is for choosing a LOCAL placeholder filename
    // before the server has issued the canonical native filename. As soon as
    // the server returns nativeFile.fileName, callers must use that value.
    public static class CadFileNamingRules
    {
        public static string GetLocalPlaceholderFileName(string partNumber)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                throw new ArgumentException("Part number is required.", "partNumber");
            }

            return partNumber.Trim() + CadConstants.IronCadPartExtension;
        }
    }
}
