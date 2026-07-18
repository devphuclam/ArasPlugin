using System;

namespace IdeaCadConnector.Core.Library
{
    /// <summary>
    /// Default client-side Part release guard. Mirrors the MVP Part lifecycle
    /// (Khoi tao -> Thiet ke chi tiet -> In Review -> Released). The PDM
    /// authority remains the source of truth for transitions.
    /// </summary>
    public sealed class DefaultPartLifecyclePolicy : IPartLifecyclePolicy
    {
        public const string KhởiTạo = "Khoi tao";
        public const string ThiếtKếChiTiết = "Thiet ke chi tiet";
        public const string InReview = "In Review";
        public const string Released = "Released";

        public bool CanRelease(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && (string.Equals(state.Trim(), InReview, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(state.Trim(), ThiếtKếChiTiết, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsReleased(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && string.Equals(state.Trim(), Released, StringComparison.OrdinalIgnoreCase);
        }
    }
}
