using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadReleaseEligibilityResult
    {
        public bool IsEligible { get; set; }
        public IReadOnlyList<string> BlockingReasons { get; set; }
    }
}
