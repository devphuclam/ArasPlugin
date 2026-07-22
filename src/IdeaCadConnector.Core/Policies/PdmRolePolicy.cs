using IdeaCadConnector.Core.Configuration;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Policies
{
    public static class PdmRolePolicy
    {
        public static bool CanExecuteCadBusinessAction(PdmUserRole role, CadBusinessActionKind action)
        {
            // The PDM Administrator has full client-side role authority. The
            // lifecycle and Aras authority checks still apply at their seams.
            if (role == PdmUserRole.PdmAdministrator)
                return true;

            if (role == PdmUserRole.DesignEngineer)
            {
                return action == CadBusinessActionKind.StartDetailedDesign
                    || action == CadBusinessActionKind.SubmitForReview;
            }

            if (role == PdmUserRole.Reviewer)
            {
                return action == CadBusinessActionKind.Approve
                    || action == CadBusinessActionKind.RequestRework;
            }

            return false;
        }

        /// <summary>
        /// Allows the configured PDM Administrator to exercise the review
        /// decision path while the development Aras workflow has no assigned
        /// reviewer yet. This is a client-side testability override; it does
        /// not grant or simulate Aras permissions.
        /// </summary>
        public static bool CanBypassReviewerAssignment(PdmUserRole role) =>
            role == PdmUserRole.PdmAdministrator;

        public static bool CanCheckout(PdmUserRole role) =>
            role == PdmUserRole.DesignEngineer || role == PdmUserRole.PdmAdministrator;

        public static bool CanCheckIn(PdmUserRole role) =>
            role == PdmUserRole.DesignEngineer || role == PdmUserRole.PdmAdministrator;

        public static bool CanCancelCheckout(PdmUserRole role) =>
            role == PdmUserRole.DesignEngineer || role == PdmUserRole.PdmAdministrator;

        public static bool CanStartNewRevision(PdmUserRole role) =>
            role == PdmUserRole.DesignEngineer || role == PdmUserRole.PdmAdministrator;
    }
}
