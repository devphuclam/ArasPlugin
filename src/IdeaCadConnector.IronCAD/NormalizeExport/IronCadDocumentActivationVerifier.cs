using System;
using System.IO;
using interop.ICApiIronCAD;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadDocumentActivationVerifier
    {
        public void Close(IZBaseApp app, IZDoc document, string errorCode)
        {
            if (app == null || document == null) throw new InvalidOperationException(errorCode);
            try { app.CloseFile(document); }
            catch (Exception ex) { throw new InvalidOperationException(errorCode, ex); }
        }

        public IZSceneDoc VerifyScene(IZBaseApp app, string expectedPath, string errorPrefix)
        {
            if (app == null) throw new InvalidOperationException(errorPrefix + "_OPEN_FAILED");
            var doc = app.ActiveDoc;
            if (doc == null) throw new InvalidOperationException(errorPrefix + "_NOT_ACTIVE");
            var scene = doc as IZSceneDoc;
            if (scene == null) throw new InvalidOperationException(errorPrefix + "_NOT_ACTIVE");
            if (string.IsNullOrWhiteSpace(doc.Name)) throw new InvalidOperationException(errorPrefix + "_PATH_UNAVAILABLE");
            string actual;
            try { actual = Path.GetFullPath(doc.Name); }
            catch (Exception ex) { throw new InvalidOperationException(errorPrefix + "_PATH_UNAVAILABLE", ex); }
            var expected = Path.GetFullPath(expectedPath ?? string.Empty);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(errorPrefix + "_PATH_MISMATCH");
            return scene;
        }
    }
}
