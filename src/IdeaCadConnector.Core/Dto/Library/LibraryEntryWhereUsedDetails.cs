using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto.Library
{
    public sealed class LibraryEntryWhereUsedDetails
    {
        public string EntryId { get; set; }

        public string PartId { get; set; }

        public string PartNumber { get; set; }

        public string PartName { get; set; }

        public IReadOnlyList<WhereUsedItemEx> WhereUsedItems { get; set; }
    }

    public sealed class WhereUsedItemEx
    {
        public string ParentPartId { get; set; }

        public string ParentPartNumber { get; set; }

        public string ParentPartName { get; set; }

        public string ParentRevision { get; set; }

        public string ParentState { get; set; }

        public int Quantity { get; set; }

        public WhereUsedSource Source { get; set; }
    }
}
