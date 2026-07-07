using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto.Library
{
    public sealed class LibraryEntryBomDetails
    {
        public string EntryId { get; set; }

        public string PartId { get; set; }

        public string PartConfigId { get; set; }

        public string PartNumber { get; set; }

        public string PartName { get; set; }

        public string Revision { get; set; }

        public IReadOnlyList<BomLineItem> BomItems { get; set; }
    }

    public sealed class BomLineItem
    {
        public string ComponentPartId { get; set; }

        public string ComponentPartNumber { get; set; }

        public string ComponentName { get; set; }

        public string ComponentRevision { get; set; }

        public int Quantity { get; set; }

        public string Unit { get; set; }
    }
}
