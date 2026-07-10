using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdeaCadConnector.Workspace
{
    public static class PushPreviewMapper
    {
        public static AnalyzeResult ToAnalyzeResult(PdmFolderAnalysis folderAnalysis, PdmBusinessStructureAnalysis businessAnalysis)
        {
            if (folderAnalysis == null)
                return null;

            var projectCode = folderAnalysis.ProjectCode ?? "UNKNOWN";

            var structureNodes = MapStructureNodes(folderAnalysis, businessAnalysis, projectCode);
            var cadFiles = MapCadFiles(folderAnalysis);
            var documentFiles = MapDocumentFiles(businessAnalysis, folderAnalysis);
            var ignoredFiles = MapIgnoredFiles(folderAnalysis);
            var warnings = MapWarnings(folderAnalysis, businessAnalysis, documentFiles);
            var summary = BuildSummary(structureNodes, cadFiles, documentFiles, ignoredFiles, warnings);

            PopulateCadLinkedPartCodes(cadFiles, structureNodes, folderAnalysis, businessAnalysis);
            PopulateDocumentLinkedPartCodes(documentFiles, structureNodes, businessAnalysis);

            return new AnalyzeResult
            {
                RepositoryCode = projectCode,
                ProjectName = projectCode,
                PackageSourcePath = businessAnalysis?.FolderPath,
                CadSourcePath = folderAnalysis.FolderPath,
                PolicyVersion = "aras01-draft-1",
                StructureNodes = structureNodes,
                CadFiles = cadFiles,
                DocumentFiles = documentFiles,
                IgnoredFiles = ignoredFiles,
                Warnings = warnings,
                Summary = summary
            };
        }

        private static IReadOnlyList<AnalyzedStructureNode> MapStructureNodes(
            PdmFolderAnalysis folderAnalysis,
            PdmBusinessStructureAnalysis businessAnalysis,
            string projectCode)
        {
            var nodes = new List<AnalyzedStructureNode>();
            var sortOrder = 0;

            if (businessAnalysis != null && businessAnalysis.HasStructure)
            {
                nodes.Add(new AnalyzedStructureNode
                {
                    LogicalCode = projectCode,
                    ParentLogicalCode = null,
                    DisplayName = projectCode,
                    NodeType = "Machine",
                    Quantity = 1,
                    SourceDocumentPath = businessAnalysis.RootDrawingFileName,
                    PrimaryCadPath = null,
                    SortOrder = sortOrder++
                });

                foreach (var rootNode in businessAnalysis.RootNodes)
                {
                    AddBusinessNode(nodes, rootNode, projectCode, ref sortOrder);
                }
            }
            else
            {
                if (folderAnalysis.PrimaryAssembly != null)
                {
                    nodes.Add(new AnalyzedStructureNode
                    {
                        LogicalCode = folderAnalysis.PrimaryAssembly.LogicalPartCode ?? "ASM",
                        ParentLogicalCode = null,
                        DisplayName = folderAnalysis.PrimaryAssembly.DisplayName ?? folderAnalysis.PrimaryAssembly.FileName,
                        NodeType = "Assembly",
                        Quantity = 1,
                        SourceDocumentPath = folderAnalysis.PrimaryAssembly.FileName,
                        PrimaryCadPath = folderAnalysis.PrimaryAssembly.FileName,
                        SortOrder = sortOrder++
                    });
                }

                foreach (var detail in folderAnalysis.DetailFiles.OrderBy(f => f.Sequence))
                {
                    nodes.Add(new AnalyzedStructureNode
                    {
                        LogicalCode = detail.LogicalPartCode ?? "DETAIL",
                        ParentLogicalCode = folderAnalysis.PrimaryAssembly?.LogicalPartCode ?? "ASM",
                        DisplayName = detail.DisplayName ?? detail.FileName,
                        NodeType = "Component",
                        Quantity = 1,
                        SourceDocumentPath = detail.FileName,
                        PrimaryCadPath = detail.FileName,
                        SortOrder = sortOrder++
                    });
                }
            }

            return nodes;
        }

        private static void AddBusinessNode(
            List<AnalyzedStructureNode> nodes,
            PdmBusinessNode businessNode,
            string parentLogicalCode,
            ref int sortOrder)
        {
            var logicalCode = businessNode.Code ?? Guid.NewGuid().ToString("N").Substring(0, 8);

            nodes.Add(new AnalyzedStructureNode
            {
                LogicalCode = logicalCode,
                ParentLogicalCode = parentLogicalCode,
                DisplayName = businessNode.DisplayName ?? businessNode.Name,
                NodeType = businessNode.NodeType ?? "Component",
                Quantity = 1,
                SourceDocumentPath = businessNode.SourceFileName,
                PrimaryCadPath = null,
                SortOrder = sortOrder++
            });

            foreach (var child in businessNode.Children)
            {
                AddBusinessNode(nodes, child, logicalCode, ref sortOrder);
            }
        }

        private static IReadOnlyList<AnalyzedCadFile> MapCadFiles(PdmFolderAnalysis folderAnalysis)
        {
            var cads = new List<AnalyzedCadFile>();
            var hasIcs = folderAnalysis.TrackedFiles.Any(f =>
                f.FileName.EndsWith(".ics", StringComparison.OrdinalIgnoreCase));

            foreach (var file in folderAnalysis.TrackedFiles)
            {
                if (hasIcs &&
                    file.FileName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var role = DetermineCadRole(file, folderAnalysis);
                cads.Add(new AnalyzedCadFile
                {
                    SourcePath = file.FullPath ?? file.RelativePath,
                    RelativePath = file.RelativePath ?? file.FileName,
                    LogicalCode = file.LogicalPartCode ?? file.ProjectCode ?? "CAD",
                    CadRole = role,
                    VersionToken = file.Version ?? "1.0",
                    Fingerprint = file.LastWriteTime.Ticks.ToString()
                });
            }

            return cads;
        }

        private static string DetermineCadRole(PdmParsedFile file, PdmFolderAnalysis folderAnalysis)
        {
            if (folderAnalysis.PrimaryAssembly != null &&
                string.Equals(file.FileName, folderAnalysis.PrimaryAssembly.FileName, StringComparison.OrdinalIgnoreCase))
                return "RootDrawing";

            if (file.NodeType == "Assembly")
                return "AssemblyRevision";

            return "PrimaryCad";
        }

        private static IReadOnlyList<AnalyzedDocumentFile> MapDocumentFiles(
            PdmBusinessStructureAnalysis businessAnalysis,
            PdmFolderAnalysis folderAnalysis = null)
        {
            var docs = new List<AnalyzedDocumentFile>();
            var businessSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (businessAnalysis != null)
            {
                foreach (var rootNode in businessAnalysis.RootNodes)
                {
                    if (!string.IsNullOrWhiteSpace(rootNode.SourceFileName))
                        businessSourcePaths.Add(rootNode.SourceFileName);

                    foreach (var child in rootNode.Children)
                    {
                        if (!string.IsNullOrWhiteSpace(child.SourceFileName))
                            businessSourcePaths.Add(child.SourceFileName);
                    }
                }

                if (!string.IsNullOrWhiteSpace(businessAnalysis.RootDrawingFileName))
                    businessSourcePaths.Add(businessAnalysis.RootDrawingFileName);
            }

            if (folderAnalysis?.DocumentFiles != null)
            {
                foreach (var docFile in folderAnalysis.DocumentFiles)
                {
                    var sourceName = Path.GetFileName(docFile.FullPath ?? docFile.RelativePath);
                    if (!string.IsNullOrWhiteSpace(sourceName) && businessSourcePaths.Contains(sourceName))
                        continue;

                    docs.Add(CreateDocumentFile(
                        folderAnalysis.FolderPath,
                        docFile.FullPath,
                        docFile.RelativePath ?? docFile.FileName,
                        docFile.LogicalPartCode ?? docFile.ProjectCode ?? "DOC",
                        docFile.NodeType == "Reference" ? "Reference" : "PackageDetail",
                        "Project"));
                }
            }

            if (businessAnalysis == null)
                return docs;

            foreach (var rootNode in businessAnalysis.RootNodes)
            {
                docs.Add(CreateDocumentFile(
                    businessAnalysis.FolderPath,
                    rootNode.SourceFileName,
                    rootNode.SourceFileName,
                    rootNode.Code,
                    "PackageGroup",
                    "Part"));

                foreach (var child in rootNode.Children)
                {
                    docs.Add(CreateDocumentFile(
                        businessAnalysis.FolderPath,
                        child.SourceFileName,
                        child.SourceFileName,
                        child.Code,
                        "PackageDetail",
                        "Part"));
                }
            }

            if (!string.IsNullOrWhiteSpace(businessAnalysis.RootDrawingFileName))
            {
                docs.Add(CreateDocumentFile(
                    businessAnalysis.FolderPath,
                    businessAnalysis.RootDrawingFileName,
                    businessAnalysis.RootDrawingFileName,
                    null,
                    "Reference",
                    "Project"));
            }

            return docs;
        }

        private static AnalyzedDocumentFile CreateDocumentFile(
            string rootFolder,
            string sourcePath,
            string relativePath,
            string logicalCode,
            string documentRole,
            string linkTargetType)
        {
            var identity = DocumentFileIdentityService.ResolveAndRead(rootFolder, sourcePath, relativePath);
            return new AnalyzedDocumentFile
            {
                SourcePath = identity.AbsolutePath,
                RelativePath = identity.RelativePath,
                LogicalCode = logicalCode,
                DocumentRole = documentRole,
                LinkTargetType = linkTargetType,
                Fingerprint = identity.FileHash,
                FileSize = identity.FileSize,
                FileFailureReason = identity.FailureReason
            };
        }

        private static IReadOnlyList<AnalyzedIgnoredFile> MapIgnoredFiles(PdmFolderAnalysis folderAnalysis)
        {
            return folderAnalysis.IgnoredFiles
                .Select(f => new AnalyzedIgnoredFile
                {
                    SourcePath = f.FullPath ?? f.RelativePath,
                    RelativePath = f.RelativePath ?? f.FileName,
                    Reason = f.Status ?? "Ignored by naming policy"
                })
                .ToList();
        }

        private static IReadOnlyList<AnalyzeWarning> MapWarnings(
            PdmFolderAnalysis folderAnalysis,
            PdmBusinessStructureAnalysis businessAnalysis,
            IReadOnlyList<AnalyzedDocumentFile> documentFiles)
        {
            var warnings = new List<AnalyzeWarning>();

            foreach (var issue in folderAnalysis.Issues)
            {
                warnings.Add(new AnalyzeWarning
                {
                    Source = issue.FileName,
                    Message = issue.Message,
                    BlocksPush = issue.BlocksPush
                });
            }

            if (businessAnalysis != null)
            {
                foreach (var issue in businessAnalysis.Issues)
                {
                    warnings.Add(new AnalyzeWarning
                    {
                        Source = issue.FileName,
                        Message = issue.Message,
                        BlocksPush = issue.BlocksPush
                    });
                }
            }
            var warnedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in documentFiles ?? Array.Empty<AnalyzedDocumentFile>())
            {
                if (string.IsNullOrWhiteSpace(document.FileFailureReason))
                    continue;

                var path = document.SourcePath ?? document.RelativePath;
                if (string.IsNullOrWhiteSpace(path) || !warnedPaths.Add(path))
                    continue;

                warnings.Add(new AnalyzeWarning
                {
                    Source = Path.GetFileName(path),
                    Message = document.FileFailureReason + " " + path,
                    BlocksPush = true
                });
            }
            return warnings;
        }

        private static AnalyzeSummary BuildSummary(
            IReadOnlyList<AnalyzedStructureNode> structureNodes,
            IReadOnlyList<AnalyzedCadFile> cadFiles,
            IReadOnlyList<AnalyzedDocumentFile> documentFiles,
            IReadOnlyList<AnalyzedIgnoredFile> ignoredFiles,
            IReadOnlyList<AnalyzeWarning> warnings)
        {
            var blockingCount = warnings.Count(w => w.BlocksPush);

            return new AnalyzeSummary
            {
                TotalStructureNodes = structureNodes.Count,
                CadFileCount = cadFiles.Count,
                DocumentFileCount = documentFiles.Count,
                IgnoredFileCount = ignoredFiles.Count,
                WarningCount = warnings.Count,
                BlockingIssueCount = blockingCount,
                IsValid = blockingCount == 0
            };
        }

        private static void PopulateCadLinkedPartCodes(
            IReadOnlyList<AnalyzedCadFile> cadFiles,
            IReadOnlyList<AnalyzedStructureNode> structureNodes,
            PdmFolderAnalysis folderAnalysis,
            PdmBusinessStructureAnalysis businessAnalysis)
        {
            if (cadFiles == null || cadFiles.Count == 0) return;

            var rootNode = structureNodes?.FirstOrDefault(n => n.ParentLogicalCode == null);
            var rootCode = rootNode?.LogicalCode;

            var seqToStructCode = new Dictionary<int, string>();
            if (businessAnalysis?.HasStructure == true)
            {
                var orderedChildren = new List<PdmBusinessNode>();
                foreach (var group in businessAnalysis.RootNodes)
                {
                    foreach (var child in group.Children)
                    {
                        orderedChildren.Add(child);
                    }
                }

                for (var i = 0; i < orderedChildren.Count; i++)
                {
                    var child = orderedChildren[i];
                    if (!string.IsNullOrWhiteSpace(child.Code))
                    {
                        // ARAS01 detail files use a flat 001..N sequence, while the business tree uses
                        // grouped codes like 01-01 / 01-02 / 02-01. Map by business package order first.
                        seqToStructCode[i + 1] = child.Code;
                    }
                }
            }

            foreach (var cad in cadFiles)
            {
                if (string.Equals(cad.CadRole, "RootDrawing", StringComparison.OrdinalIgnoreCase))
                {
                    cad.LinkedPartLogicalCode = rootCode;
                    continue;
                }

                if (seqToStructCode.Count > 0 && !string.IsNullOrWhiteSpace(cad.LogicalCode))
                {
                    var codeParts = cad.LogicalCode.Split('-');
                    if (codeParts.Length > 1 && int.TryParse(codeParts.Last(), out var seq) &&
                        seqToStructCode.TryGetValue(seq, out var linkedCode))
                    {
                        cad.LinkedPartLogicalCode = linkedCode;
                        continue;
                    }
                }

                cad.LinkedPartLogicalCode = cad.LogicalCode;
            }
        }

        private static void PopulateDocumentLinkedPartCodes(
            IReadOnlyList<AnalyzedDocumentFile> documentFiles,
            IReadOnlyList<AnalyzedStructureNode> structureNodes,
            PdmBusinessStructureAnalysis businessAnalysis)
        {
            if (documentFiles == null || documentFiles.Count == 0) return;

            var sourceToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (businessAnalysis?.HasStructure == true)
            {
                foreach (var group in businessAnalysis.RootNodes)
                {
                    if (!string.IsNullOrWhiteSpace(group.SourceFileName))
                        sourceToCode[group.SourceFileName] = group.Code;
                    foreach (var child in group.Children)
                    {
                        if (!string.IsNullOrWhiteSpace(child.SourceFileName))
                            sourceToCode[child.SourceFileName] = child.Code;
                    }
                }
            }

            foreach (var doc in documentFiles)
            {
                if (string.Equals(doc.LinkTargetType, "Project", StringComparison.OrdinalIgnoreCase))
                {
                    doc.LinkedPartLogicalCode = null;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(doc.SourcePath))
                {
                    var fileName = Path.GetFileName(doc.SourcePath);
                    if (sourceToCode.TryGetValue(fileName, out var linkedCode))
                    {
                        doc.LinkedPartLogicalCode = linkedCode;
                        continue;
                    }
                }

                var rootNode = structureNodes?.FirstOrDefault(n => n.ParentLogicalCode == null);
                doc.LinkedPartLogicalCode = rootNode?.LogicalCode;
            }
        }
    }
}
