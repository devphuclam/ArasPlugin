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

        public static WorkflowActionMapper CreateDefault()
        {
            var mapper = new WorkflowActionMapper();
            mapper.AddRule("NVTKC_Submit", "", CadBusinessActionKind.SubmitForReview);
            mapper.AddRule("Auto To In Review", "", CadBusinessActionKind.SubmitForReview);
            mapper.AddRule("TNTKC_Review", "Approve", CadBusinessActionKind.Approve);
            mapper.AddRule("TNTKC_Review", "Reject", CadBusinessActionKind.RequestRework);
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
