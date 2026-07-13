using System;
using System.Collections.Generic;
using interop.ICApiIronCAD;
using IdeaCadConnector.Workspace.BomDiagnostic;

namespace IdeaCadConnector.IronCAD.BomDiagnostic
{
    public sealed class IronCadBomDiagnosticProbe
    {
        private readonly IronCadBomDiagnosticReader _reader;

        public IronCadBomDiagnosticProbe()
            : this(new IronCadBomDiagnosticReader())
        {
        }

        internal IronCadBomDiagnosticProbe(IronCadBomDiagnosticReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public IronCadBomDiagnosticResult Run(IZBaseApp application, string outputFolder, string reportName)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("An explicit diagnostic output folder is required.", nameof(outputFolder));

            var read = _reader.Read(application);
            var analysis = BomDiagnosticTreeAnalyzer.Analyze(read.RootNode);
            foreach (var warning in read.Warnings)
                analysis.Warnings.Add(warning);

            var snapshot = new BomDiagnosticSnapshot
            {
                DocumentName = read.DocumentName,
                AuthoringToolVersion = read.AuthoringToolVersion,
                ActiveDocumentType = read.ActiveDocumentType,
                TopElementAvailable = read.TopElementAvailable,
                Analysis = analysis
            };
            var reportPath = BomDiagnosticOutput.WriteRawSnapshot(snapshot, outputFolder, reportName);
            return new IronCadBomDiagnosticResult(snapshot, reportPath);
        }
    }

    public sealed class IronCadBomDiagnosticResult
    {
        public IronCadBomDiagnosticResult(BomDiagnosticSnapshot snapshot, string reportPath)
        {
            Snapshot = snapshot;
            ReportPath = reportPath;
        }

        public BomDiagnosticSnapshot Snapshot { get; }

        public string ReportPath { get; }
    }
}
