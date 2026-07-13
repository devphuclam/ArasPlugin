using System;

namespace IdeaCadConnector.IronCAD.BomDiagnostic
{
    public static class IronCadBomDiagnosticNodeKindMapper
    {
        public static string Map(string rawKind)
        {
            if (string.IsNullOrWhiteSpace(rawKind)) return "TechnicalOrUnknown";
            switch (rawKind.Trim().ToUpperInvariant())
            {
                case "Z_ELEMENT_SCENE":
                case "Z_ELEMENT_ROOT":
                case "Z_ELEMENT_ASSEMBLY":
                case "ASSEMBLY":
                    return "Assembly";
                case "Z_ELEMENT_PART":
                case "PART":
                    return "Part";
                case "Z_ELEMENT_BREP":
                case "Z_ELEMENT_WIRE":
                case "Z_ELEMENT_PROFILE":
                case "Z_ELEMENT_SHEET_METAL":
                case "Z_ELEMENT_SHEETMETAL":
                case "Z_ELEMENT_SHEETMETAL_PART":
                case "Z_ELEMENT_SHEETMETAL_STOCK":
                case "Z_ELEMENT_SHEETMETAL_BEND":
                case "Z_ELEMENT_SHEETMETAL_PUNCH":
                case "Z_ELEMENT_SHEETMETAL_FORM":
                case "Z_ELEMENT_REFERENCE":
                case "Z_ELEMENT_TECHNICAL":
                    return "TechnicalOrUnknown";
                default:
                    return "TechnicalOrUnknown";
            }
        }
    }
}
