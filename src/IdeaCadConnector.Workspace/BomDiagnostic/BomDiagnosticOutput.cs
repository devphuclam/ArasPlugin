using System;
using System.IO;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public static class BomDiagnosticOutput
    {
        public static string WriteRawSnapshot(BomDiagnosticAnalysis analysis, string outputFolder,
            string reportName, BomDiagnosticOutputContext context)
        {
            if (analysis == null) throw new ArgumentNullException(nameof(analysis));
            return WriteRawSnapshot(new BomDiagnosticSnapshot { Analysis = analysis }, outputFolder, reportName, context);
        }

        public static string WriteRawSnapshot(BomDiagnosticSnapshot snapshot, string outputFolder,
            string reportName, BomDiagnosticOutputContext context)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("An explicit diagnostic output folder is required.", nameof(outputFolder));
            outputFolder = BomDiagnosticOutputPathPolicy.Validate(outputFolder, context);
            var safeName = SanitizeName(reportName);
            var path = Path.Combine(outputFolder, "BomDiagnostic-" + safeName + ".json");
            var raw = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(raw);
            }
            return path;
        }

        private static string SanitizeName(string reportName)
        {
            var value = string.IsNullOrWhiteSpace(reportName) ? "study" : reportName.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }
    }
}
