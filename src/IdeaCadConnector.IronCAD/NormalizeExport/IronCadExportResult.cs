using System.Collections.Generic;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadExportResult
    {
        public string RootFilePath { get; set; }

        public IDictionary<PdmSourceNode, string> SourceNodeToDefFileMap { get; set; }
    }
}
