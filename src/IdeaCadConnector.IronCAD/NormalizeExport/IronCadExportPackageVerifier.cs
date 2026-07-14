using System;
using System.Collections.Generic;
using System.Linq;
using interop.ICApiIronCAD;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadRoundTripValidationResult
    {
        public bool IsValid { get; set; }
        public IList<string> Issues { get; } = new List<string>();
    }

    public sealed class IronCadExternalReferenceRecord
    {
        public string OccurrencePath { get; set; }
        public string LinkPath { get; set; }
        public bool Resolved { get; set; }
    }

    public sealed class IronCadExportPackageVerifier
    {
        private readonly IronCadSceneNormalizationReader _reader;
        public IronCadExportPackageVerifier(IronCadSceneNormalizationReader reader) { _reader = reader ?? throw new ArgumentNullException(nameof(reader)); }

        public IronCadRoundTripValidationResult Verify(IZSceneDoc exportedScene, PdmNormalizationPlan plan)
        {
            var result = new IronCadRoundTripValidationResult();
            try
            {
                var snapshot = _reader.Read(exportedScene);
                var actual = new PdmNormalizationPlanner().CreatePlan(plan.ProjectCode, plan.Revision, snapshot.Root);
                var expected = plan.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                var observed = actual.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                if (!expected.Keys.OrderBy(x => x).SequenceEqual(observed.Keys.OrderBy(x => x))) result.Issues.Add("STAGED_TREE_MISMATCH");
                foreach (var path in expected.Keys.Intersect(observed.Keys))
                {
                    var e = expected[path]; var a = observed[path];
                    if (!string.Equals(e.NodeId, a.NodeId, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(e.ItemCode, a.ItemCode, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(e.DisplayName, a.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(e.ItemType, a.ItemType, StringComparison.OrdinalIgnoreCase))
                        result.Issues.Add("ROUND_TRIP_VALIDATION_FAILED");
                }
            }
            catch (Exception ex) { result.Issues.Add("EXPORTED_ROOT_OPEN_FAILED"); System.Diagnostics.Trace.WriteLine(ex); }
            result.IsValid = result.Issues.Count == 0;
            if (!result.IsValid) throw new InvalidOperationException(result.Issues.First());
            return result;
        }
    }
}
