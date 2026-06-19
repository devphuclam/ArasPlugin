namespace IdeaCadConnector.Core.Errors
{
    // Machine-readable error categories surfaced to the client.
    // Maps to error codes in docs/architecture/connector-aras-api-contract-draft.md.
    // The client uses these to drive UI messaging, retry behavior, and logs;
    // it does not infer rules from human-readable text.
    public enum ArasErrorCode
    {
        Unknown = 0,
        AuthInvalid,
        AuthExpired,
        PermissionDenied,
        PartNotFound,
        CadNotFound,
        CadAlreadyExists,
        CadLocked,
        CadReleasedReadOnly,
        WorkflowActionNotAvailable,
        WorkflowNoActiveProcess,
        WorkflowNoActiveTask,
        WorkflowNotAssignedToUser,
        WorkflowPathNotFound,
        WorkflowStaleContext,
        ValidationFailed,
        FileUploadNotFound,
        CheckinTransactionNotFound,
        ServerUnavailable,
        UnexpectedServerError
    }
}
