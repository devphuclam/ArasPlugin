using System;
using System.IO;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public static class BomDiagnosticOutput
    {
        public static string WriteRawSnapshot(BomDiagnosticAnalysis analysis, string outputFolder, string reportName)
        {
            if (analysis == null) throw new ArgumentNullException(nameof(analysis));
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("An explicit diagnostic output folder is required.", nameof(outputFolder));
            if (!Directory.Exists(outputFolder)) throw new DirectoryNotFoundException(outputFolder);
            var safeName = SanitizeName(reportName);
            var path = Path.Combine(outputFolder, "BomDiagnostic-" + safeName + ".json");
            var raw = JsonConvert.SerializeObject(analysis, Formatting.Indented);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("This local report may contain proprietary CAD metadata, names and paths.");
                writer.Write(raw);
            }
            return path;
        }

        public static string WriteRawSnapshot(BomDiagnosticSnapshot snapshot, string outputFolder, string reportName)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("An explicit diagnostic output folder is required.", nameof(outputFolder));
            if (!Directory.Exists(outputFolder)) throw new DirectoryNotFoundException(outputFolder);
            var safeName = SanitizeName(reportName);
            var path = Path.Combine(outputFolder, "BomDiagnostic-" + safeName + ".json");
            var raw = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine(snapshot.LocalReportWarning);
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
