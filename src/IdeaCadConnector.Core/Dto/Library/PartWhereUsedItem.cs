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
    }
}
