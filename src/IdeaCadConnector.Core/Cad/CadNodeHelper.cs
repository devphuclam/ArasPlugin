using System;

namespace IdeaCadConnector.Core.Cad
{
    public enum PdmNodeRole
    {
        Unknown,
        RootAssembly,
        AssemblyGrouping,
        Component
    }

    public static class CadNodeHelper
    {
        public static bool IsAssemblyClassification(string classification)
        {
            return !string.IsNullOrWhiteSpace(classification)
                && classification.IndexOf("Assembly", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static PdmNodeRole DetermineNodeRole(string nodeType, string partCode, string rootPartCode)
        {
            if (string.IsNullOrWhiteSpace(nodeType))
                return PdmNodeRole.Unknown;

            var isAssembly = string.Equals(nodeType, "Assembly", StringComparison.OrdinalIgnoreCase);
            var isRoot = !string.IsNullOrWhiteSpace(partCode)
                && !string.IsNullOrWhiteSpace(rootPartCode)
                && string.Equals(partCode, rootPartCode, StringComparison.OrdinalIgnoreCase);

            if (isRoot && isAssembly)
                return PdmNodeRole.RootAssembly;

            if (isAssembly)
                return PdmNodeRole.AssemblyGrouping;

            if (string.Equals(nodeType, "Component", StringComparison.OrdinalIgnoreCase))
                return PdmNodeRole.Component;

            return PdmNodeRole.Unknown;
        }

        public static string GetRootAssemblyCadHint()
        {
            return "Root assembly CAD is managed by the assembly mapping/push flow, not by component CAD creation. Select a component node or use the PDM Push flow to manage root assembly CAD.";
        }

        public static string GetAssemblySearchCadHint()
        {
            return "Assembly rows in the search screen do not use component CAD creation. Use the PDM Structure / Push flow to manage assembly CAD, or select a component part for primary component CAD.";
        }
    }
}
