namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceOptions
    {
        public string RootPath { get; set; }

        public string CompanyCode { get; set; } = "IDEA";

        public string DefaultCompanyCode => "IDEA";
    }
}
