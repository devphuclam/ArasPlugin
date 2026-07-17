using System;
using System.IO;
using interop.ICApiIronCAD;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadSceneDocumentService : IIronCadSceneDocumentService
    {
        private readonly IZBaseApp _app;
        private IZSceneDoc _openedDoc;

        public IronCadSceneDocumentService(IZBaseApp app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        public IZSceneDoc OpenDocument(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("DOCUMENT_PATH_INVALID");
            if (_openedDoc != null)
                throw new InvalidOperationException("DOCUMENT_ALREADY_OPEN");
            var doc = _app.OpenFile(filePath, false);
            if (doc == null)
                throw new InvalidOperationException("DOCUMENT_OPEN_FAILED");
            var scene = doc as IZSceneDoc;
            if (scene == null)
            {
                CloseOpenedDocument(doc);
                throw new InvalidOperationException("DOCUMENT_NOT_SCENE");
            }
            if (string.IsNullOrWhiteSpace(doc.Name) ||
                !string.Equals(Path.GetFullPath(doc.Name), Path.GetFullPath(filePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                CloseOpenedDocument(doc);
                throw new InvalidOperationException("DOCUMENT_PATH_MISMATCH");
            }
            _openedDoc = scene;
            return scene;
        }

        public void CloseDocument()
        {
            if (_openedDoc == null) return;
            var doc = _openedDoc as IZDoc;
            _openedDoc = null;
            if (doc != null) CloseOpenedDocument(doc);
        }

        public void Dispose()
        {
            CloseDocument();
        }

        private void CloseOpenedDocument(IZDoc document)
        {
            try { _app.CloseFile(document); }
            catch (Exception ex) { throw new InvalidOperationException("DOCUMENT_CLOSE_FAILED", ex); }
        }
    }
}
