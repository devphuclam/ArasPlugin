namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadExternalReferenceRecord
    {
        public string OccurrencePath { get; set; }

        public string ReportedLinkPath { get; set; }

        public string ResolvedTargetPath { get; set; }

        public bool Exists { get; set; }

        public bool InsidePackage { get; set; }

        public bool PointsToSource { get; set; }

        public bool CanonicalFileNameMatch { get; set; }
    }
}
