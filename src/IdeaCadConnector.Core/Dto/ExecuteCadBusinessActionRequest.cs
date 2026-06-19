namespace IdeaCadConnector.Core.Dto
{
    public sealed class ExecuteCadBusinessActionRequest
    {
        public ExecuteCadBusinessActionRequest(
            string cadId,
            CadBusinessActionKind action,
            string expectedModifiedOn,
            string workflowAssignmentId,
            string workflowPathId,
            string comment)
        {
            CadId = cadId;
            Action = action;
            ExpectedModifiedOn = expectedModifiedOn;
            WorkflowAssignmentId = workflowAssignmentId;
            WorkflowPathId = workflowPathId;
            Comment = comment;
        }

        public string CadId { get; }
        public CadBusinessActionKind Action { get; }
        public string ExpectedModifiedOn { get; }
        public string WorkflowAssignmentId { get; }
        public string WorkflowPathId { get; }
        public string Comment { get; }
    }
}
