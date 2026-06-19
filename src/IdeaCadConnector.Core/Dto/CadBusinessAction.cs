using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadBusinessAction
    {
        public CadBusinessAction(
            CadBusinessActionKind kind,
            string label,
            bool isAvailable,
            string unavailableReason,
            bool requiresConfirmation,
            string workflowTaskId,
            string workflowPathId)
        {
            Kind = kind;
            Label = label;
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason;
            RequiresConfirmation = requiresConfirmation;
            WorkflowTaskId = workflowTaskId;
            WorkflowPathId = workflowPathId;
        }

        public CadBusinessActionKind Kind { get; }
        public string Label { get; }
        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
        public bool RequiresConfirmation { get; }
        public string WorkflowTaskId { get; }
        public string WorkflowPathId { get; }
    }
}
