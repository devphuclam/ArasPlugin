using System;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Core.Dto.Library
{
    public sealed class PartWhereUsedItem
    {
        public string ParentPartId { get; set; }
        public string ParentPartNumber { get; set; }
        public string ParentPartName { get; set; }
        public string ParentRevision { get; set; }
        public string ParentState { get; set; }
        public int Quantity { get; set; }
        public WhereUsedSource Source { get; set; }
        public string ProjectCode { get; set; }
        public string UsedBy { get; set; }
        public string CommitId { get; set; }
        public string ActionType { get; set; }
        public DateTime? CreatedOn { get; set; }
    }
}
