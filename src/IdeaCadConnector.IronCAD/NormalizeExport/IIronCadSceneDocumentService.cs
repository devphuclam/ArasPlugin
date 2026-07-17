using System;
using interop.ICApiIronCAD;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public interface IIronCadSceneDocumentService : IDisposable
    {
        IZSceneDoc OpenDocument(string filePath);

        void CloseDocument();
    }
}
