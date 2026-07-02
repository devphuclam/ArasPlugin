namespace IdeaCadConnector.Core.Library
{
    public static class PartLibrarySchemaNames
    {
        public const string LibraryItemType = "idea_PartLibrary";
        public const string EntryRelationshipType = "idea_PartLibraryEntry";
        public const string UsageItemType = "idea_PartLibraryUsage";

        public const string LibraryStatusActive = "Active";
        public const string LibraryStatusArchived = "Archived";

        public const string EntryStatusDraft = "Draft";
        public const string EntryStatusPendingReview = "PendingReview";
        public const string EntryStatusPublished = "Published";
        public const string EntryStatusDeprecated = "Deprecated";

        public const string PartReleasedState = "Released";
    }
}
