using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadExternalReferenceValidationResult
    {
        public IList<IronCadExternalReferenceRecord> Records { get; } = new List<IronCadExternalReferenceRecord>();

        public IList<string> Issues { get; } = new List<string>();

        public bool IsValid => Issues.Count == 0;
    }

    public sealed class IronCadExternalReferenceValidator
    {
        public IronCadExternalReferenceValidationResult ValidateExportedLinks(
            IReadOnlyList<IronCadExternalReferenceRecord> rawRecords,
            PdmNormalizationPlan plan,
            IronCadExternalReferenceValidationContext context)
        {
            if (rawRecords == null) throw new ArgumentNullException(nameof(rawRecords));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var result = new IronCadExternalReferenceValidationResult();
            var allItems = new[] { plan.Root }.Concat(plan.Items).ToArray();
            foreach (var record in rawRecords.Where(r => !string.IsNullOrWhiteSpace(r.ReportedLinkPath)))
            {
                var enriched = EnrichRecord(record, context, allItems);
                result.Records.Add(enriched);
                if (!enriched.Exists)
                    result.Issues.Add("EXTERNAL_REFERENCE_MISSING at " + record.OccurrencePath);
                if (!enriched.InsidePackage)
                    result.Issues.Add("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE at " + record.OccurrencePath);
                if (enriched.PointsToSource)
                    result.Issues.Add("EXTERNAL_REFERENCE_POINTS_TO_SOURCE at " + record.OccurrencePath);
                if (!enriched.CanonicalFileNameMatch)
                    result.Issues.Add("CANONICAL_REFERENCE_MISMATCH at " + record.OccurrencePath);
            }

            var expectedFiles = new HashSet<string>(
                plan.Items.Select(item => item.CanonicalFileName),
                StringComparer.OrdinalIgnoreCase);
            // Native Save All As External can produce valid externalized
            // occurrences whose ModelLinkPath is not exposed by the current
            // IronCAD runtime. Use the package's cad directory as the source
            // of truth for definition-file presence, while retaining any
            // paths reported by the reader when available.
            var actualFiles = new HashSet<string>(
                result.Records.Where(record => record.Exists)
                    .Select(record => Path.GetFileName(record.ResolvedTargetPath)),
                StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(context.CadRoot))
            {
                foreach (var file in Directory.GetFiles(context.CadRoot, "*.ics"))
                    actualFiles.Add(Path.GetFileName(file));
            }
            foreach (var expectedFile in expectedFiles)
                if (!actualFiles.Contains(expectedFile))
                    result.Issues.Add("MISSING_EXPECTED_DEFINITION_FILE " + expectedFile);

            return result;
        }

        public IronCadExternalReferenceValidationResult Validate(
            IReadOnlyList<IronCadExternalReferenceRecord> rawRecords,
            PdmNormalizationPlan plan,
            IronCadExternalReferenceValidationContext context)
        {
            if (rawRecords == null) throw new ArgumentNullException(nameof(rawRecords));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var result = new IronCadExternalReferenceValidationResult();
            var allItems = new[] { plan.Root }.Concat(plan.Items).ToArray();

            foreach (var record in rawRecords)
            {
                var enriched = EnrichRecord(record, context, allItems);
                result.Records.Add(enriched);

                if (string.Equals(record.OccurrencePath, plan.Root?.OccurrencePath, StringComparison.Ordinal))
                    continue;

                if (!enriched.Exists)
                    result.Issues.Add("EXTERNAL_REFERENCE_MISSING at " + record.OccurrencePath);
                if (!enriched.InsidePackage)
                    result.Issues.Add("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE at " + record.OccurrencePath);
                if (enriched.PointsToSource)
                    result.Issues.Add("EXTERNAL_REFERENCE_POINTS_TO_SOURCE at " + record.OccurrencePath);
                if (!enriched.CanonicalFileNameMatch && !string.IsNullOrWhiteSpace(record.ReportedLinkPath))
                    result.Issues.Add("CANONICAL_REFERENCE_MISMATCH at " + record.OccurrencePath);
            }

            ValidateExactOccurrenceSet(rawRecords, plan, result);

            return result;
        }

        private static IronCadExternalReferenceRecord EnrichRecord(
            IronCadExternalReferenceRecord record,
            IronCadExternalReferenceValidationContext context,
            PdmPlanItem[] allItems)
        {
            var enriched = new IronCadExternalReferenceRecord
            {
                OccurrencePath = record.OccurrencePath,
                ReportedLinkPath = record.ReportedLinkPath
            };

            if (string.IsNullOrWhiteSpace(record.ReportedLinkPath))
                return enriched;

            var expected = allItems.FirstOrDefault(i =>
                string.Equals(i.OccurrencePath, record.OccurrencePath, StringComparison.Ordinal));

            var reportedFileName = Path.GetFileName(record.ReportedLinkPath);
            if (expected == null || !string.Equals(expected.CanonicalFileName, reportedFileName,
                StringComparison.OrdinalIgnoreCase))
            {
                var filenameMatch = allItems.FirstOrDefault(i =>
                    string.Equals(i.CanonicalFileName, reportedFileName, StringComparison.OrdinalIgnoreCase));
                if (filenameMatch != null) expected = filenameMatch;
            }

            var expectedFileName = expected?.CanonicalFileName;

            var evaluation = PdmExternalReferencePolicy.Evaluate(
                record.ReportedLinkPath,
                context.DocumentDirectory,
                context.CadRoot,
                context.SourceRoot,
                context.StagingRoot,
                expectedFileName);

            enriched.ResolvedTargetPath = evaluation.ResolvedTargetPath;
            enriched.Exists = !evaluation.Issues.Contains("EXTERNAL_REFERENCE_MISSING");
            enriched.InsidePackage = !evaluation.Issues.Contains("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE");
            enriched.PointsToSource = evaluation.Issues.Contains("EXTERNAL_REFERENCE_POINTS_TO_SOURCE");
            enriched.CanonicalFileNameMatch = !evaluation.Issues.Contains("CANONICAL_REFERENCE_MISMATCH");

            return enriched;
        }

        private static void ValidateExactOccurrenceSet(
            IReadOnlyList<IronCadExternalReferenceRecord> records,
            PdmNormalizationPlan plan,
            IronCadExternalReferenceValidationResult result)
        {
            var recordByPath = new Dictionary<string, IronCadExternalReferenceRecord>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (recordByPath.TryGetValue(record.OccurrencePath, out _))
                {
                    result.Issues.Add("DUPLICATE_OCCURRENCE_PATH at " + record.OccurrencePath);
                    continue;
                }
                recordByPath[record.OccurrencePath] = record;
            }

            var childItems = plan.Items.Where(i =>
                !string.Equals(i.OccurrencePath, plan.Root?.OccurrencePath, StringComparison.Ordinal)).ToArray();

            foreach (var item in childItems)
            {
                if (!recordByPath.ContainsKey(item.OccurrencePath))
                    result.Issues.Add("MISSING_EXPECTED_OCCURRENCE at " + item.OccurrencePath);
            }

            foreach (var record in records)
            {
                if (string.Equals(record.OccurrencePath, plan.Root?.OccurrencePath, StringComparison.Ordinal))
                    continue;
                if (!childItems.Any(i =>
                    string.Equals(i.OccurrencePath, record.OccurrencePath, StringComparison.Ordinal)))
                {
                    result.Issues.Add("UNEXPECTED_OCCURRENCE at " + record.OccurrencePath);
                }
            }
        }
    }
}
