using IdeaCadConnector.Aras;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class WorkflowEvaluationAmlTests
    {
        [Fact]
        public void Build_UsesActivityEvaluationContractWithRequiredWorkflowProperties()
        {
            var aml = WorkflowEvaluationAmlBuilder.Build(
                "activity-1",
                "assignment-1",
                "path-1",
                "Submit",
                "Ready for review");

            Assert.Contains("<Item type=\"Activity\" action=\"EvaluateActivity\">", aml);
            Assert.Contains("<Activity>activity-1</Activity>", aml);
            Assert.Contains("<ActivityAssignment>assignment-1</ActivityAssignment>", aml);
            Assert.Contains("<Path id=\"path-1\">Submit</Path>", aml);
            Assert.Contains("<DelegateTo>0</DelegateTo>", aml);
            Assert.Contains("<Tasks />", aml);
            Assert.Contains("<Variables />", aml);
            Assert.Contains("<Authentication mode=\"\" />", aml);
            Assert.Contains("<Comments>Ready for review</Comments>", aml);
            Assert.Contains("<Complete>1</Complete>", aml);
        }
    }
}
