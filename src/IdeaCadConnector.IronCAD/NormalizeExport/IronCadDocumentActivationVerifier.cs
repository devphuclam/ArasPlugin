using System;
using System.IO;
using interop.ICApiIronCAD;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadDocumentActivationVerifier
    {
        public void Close(IZBaseApp app, IZDoc document, string errorCode)
        {
            if (app == null || document == null) throw Failure(errorCode, "Không thể đóng IronCAD document.");
            try { app.CloseFile(document); }
            catch (Exception ex) { throw Failure(errorCode, "Không thể đóng IronCAD document.", ex); }
        }

        public IZSceneDoc VerifyScene(IZBaseApp app, string expectedPath, string errorPrefix)
        {
            if (app == null) throw Failure(errorPrefix + "_OPEN_FAILED", "Không thể mở IronCAD document.");
            var doc = app.ActiveDoc;
            if (doc == null) throw Failure(errorPrefix + "_NOT_ACTIVE", "IronCAD document vừa mở không active.");
            var scene = doc as IZSceneDoc;
            if (scene == null) throw Failure(errorPrefix + "_NOT_ACTIVE", "IronCAD document vừa mở không phải Scene.");
            if (string.IsNullOrWhiteSpace(doc.Name)) throw Failure(errorPrefix + "_PATH_UNAVAILABLE", "Không đọc được đường dẫn document vừa mở.");
            string actual;
            try { actual = Path.GetFullPath(doc.Name); }
            catch (Exception ex) { throw Failure(errorPrefix + "_PATH_UNAVAILABLE", "Đường dẫn document vừa mở không hợp lệ.", ex); }
            var expected = Path.GetFullPath(expectedPath ?? string.Empty);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw Failure(errorPrefix + "_PATH_MISMATCH", "IronCAD đang active sai document.");
            return scene;
        }

        private static PdmNormalizeExportException Failure(string code, string message, Exception inner = null)
        {
            return new PdmNormalizeExportException(code, message, inner == null ? null : inner.ToString(), inner);
        }
    }
}
