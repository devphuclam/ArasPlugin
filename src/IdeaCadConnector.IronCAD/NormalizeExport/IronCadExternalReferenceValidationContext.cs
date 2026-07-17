namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadExternalReferenceValidationContext
    {
        public string DocumentDirectory { get; set; }

        public string PackageRoot { get; set; }

        public string CadRoot { get; set; }

        public string SourceRoot { get; set; }

        public string StagingRoot { get; set; }
    }
}
