using System;

namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceLibraryReference
    {
        public string ReferenceId { get; set; }
        public string LibraryId { get; set; }
        public string LibraryEntryId { get; set; }
        public string PartId { get; set; }
        public string PartConfigId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string Revision { get; set; }
        public string ParentLogicalCode { get; set; }
        public string LocalLogicalCode { get; set; }
        public int Quantity { get; set; }
        public string RevisionPolicy { get; set; }
        public DateTime AddedOn { get; set; }
        public string AddedBy { get; set; }
    }
}
