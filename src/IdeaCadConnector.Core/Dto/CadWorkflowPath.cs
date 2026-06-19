namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadWorkflowPath
    {
        public CadWorkflowPath(string id, string name, bool isComplete)
        {
            Id = id;
            Name = name;
            IsComplete = isComplete;
        }

        public string Id { get; }
        public string Name { get; }
        public bool IsComplete { get; }
    }
}
