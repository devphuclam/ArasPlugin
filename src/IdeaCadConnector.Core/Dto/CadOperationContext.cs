using System.Collections.Generic;

namespace IdeaCadConnector.Core.Dto
{
    public sealed class CadOperationContext
    {
        public CadOperationContext(
            string cadId,
            string cadNumber,
            string cadState,
            string modifiedOn,
            bool isLocked,
            string lockOwnerId,
            string lockOwnerName,
            CadWorkflowTask activeTask,
            IReadOnlyList<CadBusinessAction> availableActions)
        {
            CadId = cadId;
            CadNumber = cadNumber;
            CadState = cadState;
            ModifiedOn = modifiedOn;
            IsLocked = isLocked;
            LockOwnerId = lockOwnerId;
            LockOwnerName = lockOwnerName;
            ActiveTask = activeTask;
            AvailableActions = availableActions;
        }

        public string CadId { get; }
        public string CadNumber { get; }
        public string CadState { get; }
        public string ModifiedOn { get; }
        public bool IsLocked { get; }
        public string LockOwnerId { get; }
        public string LockOwnerName { get; }
        public CadWorkflowTask ActiveTask { get; }
        public IReadOnlyList<CadBusinessAction> AvailableActions { get; }
    }
}
