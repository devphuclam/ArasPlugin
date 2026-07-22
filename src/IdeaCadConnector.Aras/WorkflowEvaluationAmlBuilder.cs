using System.Security;

namespace IdeaCadConnector.Aras
{
    internal static class WorkflowEvaluationAmlBuilder
    {
        public static string Build(
            string activityId,
            string assignmentId,
            string pathId,
            string pathName,
            string comment)
        {
            return "<Item type=\"Activity\" action=\"EvaluateActivity\">" +
                   "<Activity>" + Escape(activityId) + "</Activity>" +
                   "<ActivityAssignment>" + Escape(assignmentId) + "</ActivityAssignment>" +
                   "<Paths><Path id=\"" + Escape(pathId) + "\">" + Escape(pathName) + "</Path></Paths>" +
                   "<DelegateTo>0</DelegateTo>" +
                   "<Tasks />" +
                   "<Variables />" +
                   "<Authentication mode=\"\" />" +
                   "<Comments>" + Escape(comment ?? "") + "</Comments>" +
                   "<Complete>1</Complete>" +
                   "</Item>";
        }

        private static string Escape(string value)
        {
            return SecurityElement.Escape(value ?? "") ?? "";
        }
    }
}
