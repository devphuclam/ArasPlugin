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
            var warnings = MapWarnings(analyzeResult, targetBranch);
            var readiness = BuildReadiness(analyzeResult, targetBranch, warnings);

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
                var partNumber = node.ParentLogicalCode == null
                    ? (string.IsNullOrWhiteSpace(code) ? node.LogicalCode : code)
                    : (string.IsNullOrWhiteSpace(code)
                        ? node.LogicalCode
                        : code + "-" + node.LogicalCode);

                if (!string.IsNullOrWhiteSpace(node.PartNumber))
                    partNumber = node.PartNumber;

                var classification = node.NodeType switch
                {
                    "Assembly" => "Assembly",
                    "Component" => "Part",
                    "Machine" => "Assembly",
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
                    Action = string.Equals(node.SourceKind, "LibraryReference", StringComparison.OrdinalIgnoreCase)
                        ? "Reuse from Library"
                        : "Create",
                    ExistingPartId = node.ExistingPartId,
                    ExistingPartConfigId = node.ExistingPartConfigId,
                    ExistingPartRevision = node.ExistingPartRevision,
                    SourceKind = node.SourceKind,
                    LibraryEntryId = node.LibraryEntryId,
                    RevisionPolicy = node.RevisionPolicy,
                    IsExternalReference = node.IsExternalReference
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
                    SourceFilePath = cad.SourcePath,
                    LogicalCode = cad.LogicalCode,
                    CadNumber = GenerateCadNumber(result.RepositoryCode, cad),
                    Classification = classification,
                    Action = "Create",
                    LinkedPartLogicalCode = cad.LinkedPartLogicalCode
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
                    "Reference" => ClassifyReferenceDocument(doc.SourcePath),
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
                    Action = "Create",
                    LinkedPartLogicalCode = doc.LinkedPartLogicalCode
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

        private static IReadOnlyList<PreviewWarning> MapWarnings(AnalyzeResult result, string targetBranch)
        {
            var warnings = result.Warnings
                .Select(w => new PreviewWarning
                {
                    Source = w.Source,
                    Message = w.Message,
                    BlocksPush = w.BlocksPush
                })
                .ToList();

            if (!string.Equals(targetBranch, "main", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new PreviewWarning
                {
                    Source = "TargetBranch",
                    Message = $"Branch '{targetBranch ?? "-"}' is preview-only. Live push is blocked outside main.",
                    BlocksPush = true
                });
            }

            return warnings;
        }

        private static PushReadiness BuildReadiness(AnalyzeResult result, string targetBranch, IReadOnlyList<PreviewWarning> warnings)
        {
            var blockingCount = warnings.Count(w => w.BlocksPush);
            var isMainBranch = string.Equals(targetBranch, "main", StringComparison.OrdinalIgnoreCase);
            var canPush = result.Summary.IsValid && blockingCount == 0 && result.StructureNodes.Count > 0 && isMainBranch;

            string summary;
            PushReadinessLevel level;
            if (!isMainBranch)
            {
                summary = $"Branch '{targetBranch ?? "-"}' is preview-only. Switch to main before live push.";
                level = PushReadinessLevel.Blocking;
            }
            else if (canPush)
            {
                summary = $"Ready to push {result.StructureNodes.Count} part(s), {result.Summary.CadFileCount} CAD file(s), {result.Summary.DocumentFileCount} document(s).";
                level = PushReadinessLevel.Ready;
            }
            else if (blockingCount > 0)
            {
                summary = $"{blockingCount} blocking issue(s) found. Fix before push.";
                level = PushReadinessLevel.Blocking;
            }
            else if (result.StructureNodes.Count == 0)
            {
                summary = "No structure nodes were produced by Analyze. Push is blocked.";
                level = PushReadinessLevel.Blocking;
            }
            else
            {
                summary = "Push preview is incomplete. Review Analyze results before pushing.";
                level = PushReadinessLevel.Warning;
            }

            return new PushReadiness
            {
                CanPush = canPush,
                HasBlockingIssues = blockingCount > 0,
                BlockingIssueCount = blockingCount,
                Level = level,
                Summary = summary
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
                "PrimaryCad" => BuildPrimaryPartCadNumber(repositoryCode, cad),
                _ => repositoryCode + "-CAD-" + (cad.LogicalCode ?? "000")
            };
        }

        private static string BuildPrimaryPartCadNumber(string repositoryCode, AnalyzedCadFile cad)
        {
            var logicalCode = cad.LinkedPartLogicalCode ?? cad.LogicalCode;
            if (string.IsNullOrWhiteSpace(logicalCode) ||
                string.Equals(logicalCode, repositoryCode, StringComparison.OrdinalIgnoreCase))
            {
                return repositoryCode + "-ICS";
            }

            return repositoryCode + "-" + logicalCode + "-ICS";
        }

        private static string GenerateDocumentNumber(string repositoryCode, AnalyzedDocumentFile doc)
        {
            if (string.IsNullOrWhiteSpace(repositoryCode))
                return "DOC-" + BuildDocumentSuffix(doc);

            var prefix = string.IsNullOrWhiteSpace(doc.LogicalCode)
                ? "PRJ"
                : doc.LogicalCode;
            return repositoryCode + "-DOC-" + prefix + "-" + BuildDocumentSuffix(doc);
        }

        private static string ClassifyReferenceDocument(string sourcePath)
        {
            var extension = System.IO.Path.GetExtension(sourcePath ?? string.Empty);
            if (extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return "Technical Drawing";
            }

            if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return "Input/Reference";
            }

            return "Reference";
        }

        private static string BuildDocumentSuffix(AnalyzedDocumentFile doc)
        {
            var source = System.IO.Path.GetFileNameWithoutExtension(doc.SourcePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(source))
            {
                return "FILE";
            }

            var chars = source
                .ToUpperInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            var compact = new string(chars);
            while (compact.Contains("--"))
            {
                compact = compact.Replace("--", "-");
            }

            compact = compact.Trim('-');
            return string.IsNullOrWhiteSpace(compact) ? "FILE" : compact;
        }
    }
}
