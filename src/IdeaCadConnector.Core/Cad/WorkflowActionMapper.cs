using System;
using System.Collections.Generic;
using System.Diagnostics;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Cad
{
    public sealed class WorkflowActionMapper
    {
        private readonly List<WorkflowActionRule> _rules;
        private readonly Action<string> _logWarning;

        public WorkflowActionMapper(Action<string> logWarning = null)
        {
            _rules = new List<WorkflowActionRule>();
            _logWarning = logWarning ?? (msg => Debug.WriteLine(msg));
        }

        public void AddRule(string activityName, string pathNamePattern, CadBusinessActionKind actionKind)
        {
            _rules.Add(new WorkflowActionRule
            {
                ActivityName = activityName ?? "",
                PathNamePattern = pathNamePattern ?? "",
                ActionKind = actionKind
            });
        }

        public CadBusinessActionKind? Map(string activityName, string pathName)
        {
            var activity = (activityName ?? "").Trim();
            var path = (pathName ?? "").Trim();

            foreach (var rule in _rules)
            {
                if (!string.Equals(activity, rule.ActivityName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (rule.PathNamePattern.Length == 0
                    || path.IndexOf(rule.PathNamePattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return rule.ActionKind;
                }
            }

            _logWarning($"Unrecognized workflow path: activity={activityName} path={pathName}");

            return null;
        }

        /// <summary>
        /// Resolves the single workflow path exposed by Aras for a CAD that is
        /// still in detailed design. Some Aras workflow activities expose a
        /// valid submit path without a stable activity/path name that can be
        /// mapped by the client. The state and cardinality checks keep this
        /// fallback narrow and prevent it from being used for review actions.
        /// </summary>
        public static CadBusinessActionKind? InferSingleOpenPathAction(
            string cadState,
            IReadOnlyList<CadWorkflowPath> paths)
        {
            if (!CadLifecyclePolicy.CanSubmitForReview(cadState) || paths == null)
                return null;

            var openPathCount = 0;
            foreach (var path in paths)
            {
                if (path != null && !path.IsComplete && !string.IsNullOrWhiteSpace(path.Id))
                    openPathCount++;
            }

            return openPathCount == 1
                ? CadBusinessActionKind.SubmitForReview
                : (CadBusinessActionKind?)null;
        }

        public static WorkflowActionMapper CreateDefault()
        {
            var mapper = new WorkflowActionMapper();
            mapper.AddRule("Auto To In Review", "", CadBusinessActionKind.SubmitForReview);
            mapper.AddRule("Withdraw", "", CadBusinessActionKind.Withdraw);
            return mapper;
        }

        private sealed class WorkflowActionRule
        {
            public string ActivityName { get; set; }
            public string PathNamePattern { get; set; }
            public CadBusinessActionKind ActionKind { get; set; }
        }
    }
}
