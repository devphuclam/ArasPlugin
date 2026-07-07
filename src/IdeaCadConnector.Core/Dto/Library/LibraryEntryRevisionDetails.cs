using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto.Library
{
    public sealed class LibraryEntryRevisionDetails
    {
        public string EntryId { get; set; }

        public string PartConfigId { get; set; }

        public string CurrentPartId { get; set; }

        public string CurrentRevision { get; set; }

        public string CurrentLifecycleState { get; set; }

        public string CurrentGeneration { get; set; }

        public bool HasNewerReleasedRevision { get; set; }

        public IReadOnlyList<RevisionHistoryItem> RevisionHistory { get; set; }
    }

    public sealed class RevisionHistoryItem
    {
        public string PartId { get; set; }

        public string Revision { get; set; }

        public string LifecycleState { get; set; }

        public string Generation { get; set; }

        public string ModifiedOn { get; set; }

        public bool IsCurrent { get; set; }
    }
}
