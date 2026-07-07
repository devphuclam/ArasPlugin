namespace IdeaCadConnector.Core.Dto.Library
{
    public sealed class LibraryEntryDetailBundle
    {
        public PartLibraryEntryDetails Entry { get; set; }

        public LibraryEntryCadDetails Cad { get; set; }

        public LibraryEntryBomDetails Bom { get; set; }

        public LibraryEntryRevisionDetails Revisions { get; set; }

        public LibraryEntryWhereUsedDetails WhereUsed { get; set; }
    }
}
