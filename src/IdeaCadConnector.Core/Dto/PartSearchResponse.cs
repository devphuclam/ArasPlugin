using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto
{
    public sealed class PartSearchResponse
    {
        public PartSearchResponse(IReadOnlyList<PartSearchResult> items, int totalCount)
        {
            Items = items;
            TotalCount = totalCount;
        }

        public IReadOnlyList<PartSearchResult> Items { get; }

        public int TotalCount { get; }
    }
}