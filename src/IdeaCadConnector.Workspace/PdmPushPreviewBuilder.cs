using System;
using System.Collections.Generic;
using System.Linq;

namespace IdeaCadConnector.Workspace
{
    public interface IPdmPushPreviewBuilder
    {
        PushPreview Build(AnalyzeResult analyzeResult, string targetBranch, string commitMessage);
    }

    public sealed class PdmPushPreviewBuilder : IPdmPushPreviewBuilder
    {
        public PushPreview Build(AnalyzeResult analyzeResult, string targetBranch, string commitMessage)
        {
            if (analyzeResult == null)
                throw new ArgumentNullException(nameof(analyzeResult));

            var parts = MapParts(analyzeResult);
            var cads = MapCads(analyzeResult);
            var documents = MapDocuments(analyzeResult);
            var ignored = MapIgnored(analyzeResult);
            var warnings = MapWarnings(analyzeResult);
            var readiness = BuildReadiness(analyzeResult, warnings);

            return new PushPreview
            {
                RepositoryCode = analyzeResult.RepositoryCode,
                ProjectName = analyzeResult.ProjectName,
                TargetBranch = targetBranch ?? "main",
                CommitMessage = commitMessage ?? string.Empty,
                Parts = parts,
                Cads = cads,
                Documents = documents,
                IgnoredFiles = ignored,
                Warnings = warnings,
                Readiness = readiness
            };
        }

        private static IReadOnlyList<PartPreviewRow> MapParts(AnalyzeResult result)
        {
            var rows = new List<PartPreviewRow>();
            var code = result.RepositoryCode;

            foreach (var node in result.StructureNodes)
            {
                var partNumber = string.IsNullOrWhiteSpace(code)
                    ? node.LogicalCode
                    : code + "-" + node.LogicalCode;

                var classification = node.NodeType switch
                {
                    "Assembly" => "Assembly",
                    "Component" => "Fabricated Part",
                    "Machine" => "Machine",
                    _ => "Part"
                };

                rows.Add(new PartPreviewRow
                {
                    LogicalCode = node.LogicalCode,
                    ParentLogicalCode = node.ParentLogicalCode,
                    PartNumber = partNumber,
                    Name = node.DisplayName,
                    Classification = classification,
                    Quantity = node.Quantity,
                    Action = "Create"
                });
            }

            return rows;
        }

        private static IReadOnlyList<CadPreviewRow> MapCads(AnalyzeResult result)
        {
            var rows = new List<CadPreviewRow>();
            var structureLookup = result.StructureNodes
                .GroupBy(n => n.LogicalCode)
                .ToDictionary(g => g.Key, g => g.First().NodeType, StringComparer.OrdinalIgnoreCase);

            foreach (var cad in result.CadFiles)
            {
                var classification = ClassifyCad(cad, structureLookup);

                rows.Add(new CadPreviewRow
                {
                    SourceFileName = System.IO.Path.GetFileName(cad.SourcePath),
                    LogicalCode = cad.LogicalCode,
                    CadNumber = GenerateCadNumber(result.RepositoryCode, cad),
                    Classification = classification,
                    Action = "Create"
                });
            }

            return rows;
        }

        private static string ClassifyCad(AnalyzedCadFile cad, Dictionary<string, string> structureLookup)
        {
            if (cad.CadRole == "RootDrawing")
                return "Mechanical/Assembly";

            if (!string.IsNullOrWhiteSpace(cad.LogicalCode) &&
                structureLookup.TryGetValue(cad.LogicalCode, out var nodeType))
            {
                if (nodeType == "Assembly" || nodeType == "Machine")
                    return "Mechanical/Assembly";
            }

            return cad.CadRole switch
            {
                "AssemblyRevision" => "Mechanical/Drawing",
                _ => "Mechanical/Part"
            };
        }

        private static IReadOnlyList<DocumentPreviewRow> MapDocuments(AnalyzeResult result)
        {
            var rows = new List<DocumentPreviewRow>();

            foreach (var doc in result.DocumentFiles)
            {
                var classification = doc.DocumentRole switch
                {
                    "PackageGroup" => "Package Manifest",
                    "PackageDetail" => "Technical Drawing",
                    "Reference" => "Input/Reference",
                    _ => "Document"
                };

                var linkTarget = doc.LinkTargetType ?? (string.IsNullOrWhiteSpace(doc.LogicalCode) ? "Project" : "Part");

                rows.Add(new DocumentPreviewRow
                {
                    SourceFileName = System.IO.Path.GetFileName(doc.SourcePath),
                    LogicalCode = doc.LogicalCode,
                    DocumentNumber = GenerateDocumentNumber(result.RepositoryCode, doc),
                    Classification = classification,
                    LinkTargetType = linkTarget,
                    Action = "Create"
                });
            }

            return rows;
        }

        private static IReadOnlyList<IgnoredPreviewRow> MapIgnored(AnalyzeResult result)
        {
            return result.IgnoredFiles
                .Select(ignored => new IgnoredPreviewRow
                {
                    SourceFileName = System.IO.Path.GetFileName(ignored.SourcePath),
                    Reason = ignored.Reason
                })
                .ToList();
        }

        private static IReadOnlyList<PreviewWarning> MapWarnings(AnalyzeResult result)
        {
            return result.Warnings
                .Select(w => new PreviewWarning
                {
                    Source = w.Source,
                    Message = w.Message,
                    BlocksPush = w.BlocksPush
                })
                .ToList();
        }

        private static PushReadiness BuildReadiness(AnalyzeResult result, IReadOnlyList<PreviewWarning> warnings)
        {
            var blockingCount = warnings.Count(w => w.BlocksPush);
            var canPush = result.Summary.IsValid && blockingCount == 0 && result.StructureNodes.Count > 0;

            return new PushReadiness
            {
                CanPush = canPush,
                HasBlockingIssues = blockingCount > 0,
                BlockingIssueCount = blockingCount,
                Summary = canPush
                    ? $"Ready to push {result.StructureNodes.Count} part(s), {result.Summary.CadFileCount} CAD file(s), {result.Summary.DocumentFileCount} document(s)."
                    : $"{blockingCount} blocking issue(s) found. Fix before push."
            };
        }

        private static string GenerateCadNumber(string repositoryCode, AnalyzedCadFile cad)
        {
            if (string.IsNullOrWhiteSpace(repositoryCode))
                return cad.LogicalCode ?? "CAD";

            return cad.CadRole switch
            {
                "RootDrawing" => repositoryCode + "-CAD-ASM",
                "AssemblyRevision" => repositoryCode + "-CAD-" + (cad.LogicalCode ?? "REV"),
                "PrimaryCad" => repositoryCode + "-CAD-" + (cad.VersionToken ?? "000"),
                _ => repositoryCode + "-CAD-" + (cad.LogicalCode ?? "000")
            };
        }

        private static string GenerateDocumentNumber(string repositoryCode, AnalyzedDocumentFile doc)
        {
            if (string.IsNullOrWhiteSpace(repositoryCode))
                return "DOC-001";

            var prefix = string.IsNullOrWhiteSpace(doc.LogicalCode) ? "PRJ" : doc.LogicalCode;
            return repositoryCode + "-DOC-" + prefix;
        }
    }
}
