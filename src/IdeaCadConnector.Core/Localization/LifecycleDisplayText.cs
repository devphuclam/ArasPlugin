using System.Globalization;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Localization
{
    public static class LifecycleDisplayText
    {
        private static string L(string key) =>
            TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, key);

        public static string GetStateCategoryLabel(string state)
        {
            var raw = CadLifecyclePolicy.GetStateCategory(state);
            switch (raw)
            {
                case "Unknown": return L(TranslationKeys.StateUnknown);
                case "Editable": return L(TranslationKeys.StateEditable);
                case "ReviewOnly": return L(TranslationKeys.StateReviewOnly);
                case "ReadOnly": return L(TranslationKeys.StateReadOnly);
                case "Inactive": return L(TranslationKeys.StateInactive);
                default: return raw;
            }
        }

        public static string GetStateSummary(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return L(TranslationKeys.GuidanceMissing);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Initial))
                return L(TranslationKeys.SummaryInitial);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.DetailedDesign))
                return L(TranslationKeys.SummaryDetailedDesign);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InReview))
                return L(TranslationKeys.SummaryInReview);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released))
                return L(TranslationKeys.SummaryReleased);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InChange))
                return L(TranslationKeys.SummaryInChange);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Superseded)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Obsolete))
                return L(TranslationKeys.SummarySuperseded);

            return string.Format(L(TranslationKeys.SummaryUnknown), state);
        }

        public static string GetActionGuidance(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return L(TranslationKeys.GuidanceMissing);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Initial))
                return L(TranslationKeys.GuidanceInitial);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.DetailedDesign))
                return L(TranslationKeys.GuidanceDetailedDesign);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InReview))
                return L(TranslationKeys.GuidanceInReview);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released))
                return L(TranslationKeys.GuidanceReleased);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InChange))
                return L(TranslationKeys.GuidanceInChange);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Superseded)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Obsolete))
                return L(TranslationKeys.GuidanceSuperseded);

            return L(TranslationKeys.GuidanceDefault);
        }

        public static string GetWorkflowIdleText(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return L(TranslationKeys.WorkflowIdleDefault);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Initial))
                return L(TranslationKeys.WorkflowIdleInitial);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.DetailedDesign))
                return L(TranslationKeys.WorkflowIdleDetailedDesign);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InReview))
                return L(TranslationKeys.WorkflowIdleInReview);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released))
                return L(TranslationKeys.WorkflowIdleReleased);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InChange))
                return L(TranslationKeys.WorkflowIdleInChange);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Superseded)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Obsolete))
                return L(TranslationKeys.WorkflowIdleSuperseded);

            return L(TranslationKeys.WorkflowIdleDefault);
        }

        public static string GetCheckoutBlockedMessage(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return L(TranslationKeys.CheckoutBlockedUnknownState);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InReview)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InChange)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Superseded)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Obsolete))
            {
                return GetActionGuidance(state);
            }

            return string.Format(L(TranslationKeys.CheckoutBlockedFallback), state);
        }

        public static string GetBusinessActionBlockedMessage(CadBusinessActionKind kind, string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return L(TranslationKeys.GuidanceMissing);

            switch (kind)
            {
                case CadBusinessActionKind.StartDetailedDesign:
                    if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Initial))
                        return GetActionGuidance(state);
                    if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.DetailedDesign))
                        return GetActionGuidance(state);
                    return GetStateSummary(state);

                case CadBusinessActionKind.SubmitForReview:
                case CadBusinessActionKind.Approve:
                case CadBusinessActionKind.RequestRework:
                    return GetActionGuidance(state);

                default:
                    return GetStateSummary(state);
            }
        }

        public static string GetStaleSessionLabel()
        {
            return L(TranslationKeys.CadLockStateStaleSession);
        }

        public static string GetStaleSessionMessage(string liveState)
        {
            if (string.IsNullOrWhiteSpace(liveState))
                return L(TranslationKeys.CheckoutStaleState);

            return string.Concat(L(TranslationKeys.CheckoutStaleState), " ", GetActionGuidance(liveState));
        }

        public static string GetDifferentUserSessionMessage(string lockedBy)
        {
            if (string.IsNullOrWhiteSpace(lockedBy))
                return L(TranslationKeys.SessionOwnedByOtherUser);

            return string.Format(L(TranslationKeys.SessionOwnedByDifferentUser), lockedBy);
        }

        public static string GetCheckedOutByMeFileMissingStaleLabel()
        {
            return L(TranslationKeys.CadLockStateStaleSession);
        }

        public static string GetReadOnlySessionStaleMessage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return L(TranslationKeys.ReadOnlySessionStale);

            return string.Format(L(TranslationKeys.ReadOnlySessionStaleWithFile), fileName);
        }

        public static string GetPushSessionStaleMessage(string liveState)
        {
            return GetStaleSessionMessage(liveState);
        }

        public static string GetRevisionDriftMessage(string lastKnownRevision, string liveRevision)
        {
            return string.Format(
                L(TranslationKeys.RevisionDriftDetected),
                lastKnownRevision,
                liveRevision);
        }

        public static string GetGenerationDriftMessage(long lastKnownGeneration, long liveGeneration)
        {
            return string.Format(
                L(TranslationKeys.GenerationDriftDetected),
                lastKnownGeneration,
                liveGeneration);
        }
    }
}
