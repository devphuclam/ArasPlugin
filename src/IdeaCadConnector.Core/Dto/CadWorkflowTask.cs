using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadWorkflowTask
    {
        public CadWorkflowTask(
            string assignmentId,
            string activityId,
            string activityName,
            string workflowProcessId,
            string workflowProcessState,
            string assigneeName,
            IReadOnlyList<CadWorkflowPath> availablePaths)
        {
            AssignmentId = assignmentId;
            ActivityId = activityId;
            ActivityName = activityName;
            WorkflowProcessId = workflowProcessId;
            WorkflowProcessState = workflowProcessState;
            AssigneeName = assigneeName;
            AvailablePaths = availablePaths;
        }

        public string AssignmentId { get; }
        public string ActivityId { get; }
        public string ActivityName { get; }
        public string WorkflowProcessId { get; }
        public string WorkflowProcessState { get; }
        public string AssigneeName { get; }
        public IReadOnlyList<CadWorkflowPath> AvailablePaths { get; }
    }
}
