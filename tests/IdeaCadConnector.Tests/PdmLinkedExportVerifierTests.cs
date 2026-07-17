using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdeaCadConnector.IronCAD.NormalizeExport;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmLinkedExportVerifierTests
    {
        [Fact]
        public void Validator_DetectsLinkOutsideCadRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-val-" + Guid.NewGuid().ToString("N"));
            var cad = Directory.CreateDirectory(Path.Combine(root, "cad")).FullName;
            var outside = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
            var outsideFile = Path.Combine(outside, "outside.ics");
            File.WriteAllText(outsideFile, "content");

            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord
                {
                    OccurrencePath = "0/0",
                    ReportedLinkPath = outsideFile
                }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext
            {
                DocumentDirectory = root,
                CadRoot = cad,
                SourceRoot = Path.Combine(root, "source"),
                StagingRoot = Path.Combine(root, "staging")
            };

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.Contains(result.Issues, i => i.Contains("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE"));

            Directory.Delete(root, true);
        }

        [Fact]
        public void Validator_DetectsMissingTargetFile()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-val-" + Guid.NewGuid().ToString("N"));
            var cad = Directory.CreateDirectory(Path.Combine(root, "cad")).FullName;

            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord
                {
                    OccurrencePath = "0/0",
                    ReportedLinkPath = Path.Combine(cad, "nonexistent.ics")
                }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext
            {
                DocumentDirectory = root,
                CadRoot = cad
            };

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.Contains(result.Issues, i => i.Contains("EXTERNAL_REFERENCE_MISSING"));

            Directory.Delete(root, true);
        }

        [Fact]
        public void Validator_DetectsCanonicalFileNameMismatch()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-val-" + Guid.NewGuid().ToString("N"));
            var cad = Directory.CreateDirectory(Path.Combine(root, "cad")).FullName;
            var wrongFile = Path.Combine(cad, "WRONG__NAME.ics");
            File.WriteAllText(wrongFile, "content");

            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord
                {
                    OccurrencePath = "0/0",
                    ReportedLinkPath = wrongFile
                }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext
            {
                DocumentDirectory = root,
                CadRoot = cad
            };

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.Contains(result.Issues, i => i.Contains("CANONICAL_REFERENCE_MISMATCH"));

            Directory.Delete(root, true);
        }

        [Fact]
        public void Validator_NullLinkOnNonRoot_ReportsMissingLink()
        {
            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord
                {
                    OccurrencePath = "0/0",
                    ReportedLinkPath = null
                }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext();

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.Contains(result.Issues, i => i.Contains("EXTERNAL_REFERENCE_MISSING") || i.Contains("MISSING"));
        }

        [Fact]
        public void Validator_ExactOccurrenceSetMatch_Passes()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-val-" + Guid.NewGuid().ToString("N"));
            var cad = Directory.CreateDirectory(Path.Combine(root, "cad")).FullName;
            var childFile = Path.Combine(cad, "PDM-TEST__A01__CHILD.ics");
            File.WriteAllText(childFile, "content");

            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord { OccurrencePath = "0", ReportedLinkPath = null },
                new IronCadExternalReferenceRecord { OccurrencePath = "0/0", ReportedLinkPath = childFile }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext
            {
                DocumentDirectory = root,
                CadRoot = cad
            };

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.True(result.IsValid, string.Join("; ", result.Issues));

            Directory.Delete(root, true);
        }

        [Fact]
        public void Validator_ResolvesRelativeLinkFromOpenedRootDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-val-" + Guid.NewGuid().ToString("N"));
            var cad = Directory.CreateDirectory(Path.Combine(root, "cad")).FullName;
            File.WriteAllText(Path.Combine(cad, "PDM-TEST__A01__CHILD.ics"), "content");

            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord { OccurrencePath = "0", ReportedLinkPath = null },
                new IronCadExternalReferenceRecord
                {
                    OccurrencePath = "0/0",
                    ReportedLinkPath = "PDM-TEST__A01__CHILD.ics"
                }
            };

            var result = new IronCadExternalReferenceValidator().Validate(
                records,
                CreatePlan(),
                new IronCadExternalReferenceValidationContext
                {
                    DocumentDirectory = cad,
                    CadRoot = cad
                });

            Assert.True(result.IsValid, string.Join("; ", result.Issues));
            Directory.Delete(root, true);
        }

        [Fact]
        public void Validator_MissingExpectedOccurrence_Fails()
        {
            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord { OccurrencePath = "0", ReportedLinkPath = null }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext();

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.Contains(result.Issues, i => i.Contains("MISSING_EXPECTED_OCCURRENCE"));
        }

        [Fact]
        public void Validator_UnexpectedOccurrence_Fails()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-val-" + Guid.NewGuid().ToString("N"));
            var cad = Directory.CreateDirectory(Path.Combine(root, "cad")).FullName;

            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord { OccurrencePath = "0", ReportedLinkPath = null },
                new IronCadExternalReferenceRecord { OccurrencePath = "0/0", ReportedLinkPath = null },
                new IronCadExternalReferenceRecord { OccurrencePath = "0/999", ReportedLinkPath = null }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext { DocumentDirectory = root, CadRoot = cad };

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.Contains(result.Issues, i => i.Contains("UNEXPECTED_OCCURRENCE"));

            Directory.Delete(root, true);
        }

        [Fact]
        public void Validator_DuplicateOccurrencePath_Fails()
        {
            var records = new List<IronCadExternalReferenceRecord>
            {
                new IronCadExternalReferenceRecord { OccurrencePath = "0", ReportedLinkPath = null },
                new IronCadExternalReferenceRecord { OccurrencePath = "0/0", ReportedLinkPath = null },
                new IronCadExternalReferenceRecord { OccurrencePath = "0/0", ReportedLinkPath = null }
            };

            var plan = CreatePlan();
            var context = new IronCadExternalReferenceValidationContext();

            var validator = new IronCadExternalReferenceValidator();
            var result = validator.Validate(records, plan, context);

            Assert.Contains(result.Issues, i => i.Contains("DUPLICATE_OCCURRENCE_PATH"));
        }

        private static PdmNormalizationPlan CreatePlan()
        {
            var plan = new PdmNormalizationPlan { ProjectCode = "PDM-TEST", Revision = "A" };
            plan.Root = new PdmPlanItem
            {
                OccurrencePath = "0", NodeId = "root", ItemCode = "ROOT", ItemType = "ASM",
                DisplayName = "ROOT", SceneName = "ROOT", ProjectCode = "PDM-TEST", Revision = "A",
                SourceKind = PdmNodeKind.SceneRoot
            };
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = "0/0", ParentNodeId = "root", NodeId = "child", ItemCode = "A01",
                ItemType = "PRT", DisplayName = "CHILD", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1,
                CanonicalFileName = "PDM-TEST__A01__CHILD.ics"
            });
            return plan;
        }
    }
}
