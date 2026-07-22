using System;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Cad
{
    public static class WorkflowActionExecutionPolicy
    {
        public static bool UsesWorkflowAssignment(
            CadBusinessActionKind action,
            string workflowAssignmentId,
            string workflowPathId)
        {
            return action == CadBusinessActionKind.SubmitForReview
                && !string.IsNullOrWhiteSpace(workflowAssignmentId)
                && !string.IsNullOrWhiteSpace(workflowPathId);
        }
    }
}
