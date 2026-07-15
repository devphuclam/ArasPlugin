using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.Workspace.Clone
{
    public sealed class PdmClonePackageInput
    {
        public string PackageRoot { get; set; }
        public string ProjectCode { get; set; }
        public string Revision { get; set; }
        public string BranchName { get; set; }
        public string RootNodeId { get; set; }
        public IEnumerable<PdmCloneNode> Nodes { get; set; } = new PdmCloneNode[0];
        public IEnumerable<PdmCloneBomEdge> Edges { get; set; } = new PdmCloneBomEdge[0];
    }

    public sealed class PdmCloneNode
    {
        public string NodeId { get; set; }
        public string ItemCode { get; set; }
        public string ItemType { get; set; }
        public string DisplayName { get; set; }
        public string Revision { get; set; }
        public string NativeFileName { get; set; }
    }

    public sealed class PdmCloneBomEdge
    {
        public string ParentNodeId { get; set; }
        public string ChildNodeId { get; set; }
        public decimal Quantity { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class PdmClonePackageBuildResult
    {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public PdmPackageManifest Manifest { get; private set; }

        public static PdmClonePackageBuildResult Ok(PdmPackageManifest manifest)
        {
            return new PdmClonePackageBuildResult { Success = true, Manifest = manifest };
        }

        public static PdmClonePackageBuildResult Fail(string errorMessage)
        {
            return new PdmClonePackageBuildResult { ErrorMessage = errorMessage ?? "Clone package build failed." };
        }
    }

    public sealed class PdmClonePackageBuilder
    {
        public PdmClonePackageBuildResult Build(PdmClonePackageInput input)
        {
            if (input == null)
                return PdmClonePackageBuildResult.Fail("Clone package input is required.");

            PdmCloneNode[] inputNodes;
            PdmCloneBomEdge[] inputEdges;
            try
            {
                inputNodes = (input.Nodes ?? Array.Empty<PdmCloneNode>()).ToArray();
                inputEdges = (input.Edges ?? Array.Empty<PdmCloneBomEdge>()).ToArray();
            }
            catch (Exception ex)
            {
                return PdmClonePackageBuildResult.Fail("Clone package input could not be enumerated: " + ex.Message);
            }

            try
            {
                var nodes = ValidateInput(input, inputNodes, inputEdges);
                var orderedEdges = inputEdges
                    .OrderBy(edge => edge.ParentNodeId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(edge => edge.SortOrder)
                    .ThenBy(edge => edge.ChildNodeId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var occurrences = BuildOccurrences(input.RootNodeId, orderedEdges);
                if (occurrences.Select(occurrence => occurrence.NodeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != nodes.Count)
                    return PdmClonePackageBuildResult.Fail("Clone package contains definitions not reachable from the root node.");

                var rootNode = nodes[input.RootNodeId];
                var manifest = new PdmPackageManifest
                {
                    SchemaVersion = 2,
                    ProjectCode = PdmNameNormalizer.NormalizeProjectCode(input.ProjectCode),
                    Revision = input.Revision,
                    RootNodeId = input.RootNodeId,
                    RootItemCode = rootNode.ItemCode,
                    RootOccurrenceId = occurrences.Single(occurrence => occurrence.Manifest.ParentOccurrenceId == null).Manifest.OccurrenceId,
                    RootFile = ToCadRelativePath(rootNode.NativeFileName),
                    Definitions = nodes.Values
                        .OrderBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                        .Select(node => ToDefinition(node, input.Revision))
                        .ToArray(),
                    Occurrences = occurrences.Select(occurrence => occurrence.Manifest).ToArray(),
                    BomV2 = ToBomEdges(occurrences),
                    Warnings = new string[0]
                };

                var manifestPath = Path.Combine(input.PackageRoot, PdmPackageImportReader.ManifestFileName);
                var metadataDirectory = Path.Combine(input.PackageRoot, ".idea-pdm");
                var branchRegistryPath = Path.Combine(metadataDirectory, "branches.json");
                var manifestCreated = !File.Exists(manifestPath);
                var branchRegistryCreated = !File.Exists(branchRegistryPath);
                var metadataDirectoryCreated = !Directory.Exists(metadataDirectory);
                File.WriteAllText(
                    manifestPath,
                    new PdmPackageManifestWriter().Serialize(manifest));
                WriteBranches(input.PackageRoot, input.BranchName);

                var validation = new PdmPackageValidator().Validate(input.PackageRoot, manifest);
                if (validation.IsValid)
                    return PdmClonePackageBuildResult.Ok(manifest);

                RemoveCreatedArtifacts(manifestPath, manifestCreated, branchRegistryPath, branchRegistryCreated,
                    metadataDirectory, metadataDirectoryCreated);
                return PdmClonePackageBuildResult.Fail("Clone package validation failed: " + string.Join(", ", validation.Issues));
            }
            catch (ArgumentException ex)
            {
                return PdmClonePackageBuildResult.Fail(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return PdmClonePackageBuildResult.Fail(ex.Message);
            }
        }

        private static IDictionary<string, PdmCloneNode> ValidateInput(
            PdmClonePackageInput input,
            PdmCloneNode[] nodeList,
            PdmCloneBomEdge[] edges)
        {
            if (string.IsNullOrWhiteSpace(input.PackageRoot) || !Directory.Exists(input.PackageRoot))
                throw new ArgumentException("PackageRoot must be an existing directory.", nameof(input));
            if (string.IsNullOrWhiteSpace(input.ProjectCode))
                throw new ArgumentException("ProjectCode is required.", nameof(input));
            if (string.IsNullOrWhiteSpace(input.Revision))
                throw new ArgumentException("Revision is required.", nameof(input));
            if (string.IsNullOrWhiteSpace(input.BranchName))
                throw new ArgumentException("BranchName is required.", nameof(input));
            if (string.IsNullOrWhiteSpace(input.RootNodeId))
                throw new ArgumentException("RootNodeId is required.", nameof(input));

            if (nodeList.Length == 0 || nodeList.Any(node => node == null || string.IsNullOrWhiteSpace(node.NodeId)))
                throw new ArgumentException("Each clone node requires a NodeId.", nameof(input));
            if (nodeList.GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                throw new ArgumentException("Clone node NodeId values must be unique.", nameof(input));

            var nodes = nodeList.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            if (!nodes.ContainsKey(input.RootNodeId))
                throw new ArgumentException("RootNodeId does not identify a clone node.", nameof(input));

            foreach (var node in nodeList)
            {
                if (string.IsNullOrWhiteSpace(node.ItemCode) || string.IsNullOrWhiteSpace(node.ItemType) || string.IsNullOrWhiteSpace(node.DisplayName))
                    throw new ArgumentException("Each clone node requires ItemCode, ItemType, and DisplayName.", nameof(input));
                ValidateNativeFileName(node.NativeFileName);
                var cadPath = Path.Combine(input.PackageRoot, "cad", node.NativeFileName);
                if (!File.Exists(cadPath))
                    throw new ArgumentException("Native CAD file is missing: " + node.NativeFileName, nameof(input));
            }

            if (nodeList.GroupBy(node => node.NativeFileName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                throw new ArgumentException("Clone node NativeFileName values must not be duplicate.", nameof(input));

            if (edges.Any(edge => edge == null || !nodes.ContainsKey(edge.ParentNodeId ?? string.Empty) || !nodes.ContainsKey(edge.ChildNodeId ?? string.Empty)))
                throw new ArgumentException("Each BOM edge must refer to clone nodes.", nameof(input));
            if (edges.Any(edge => edge.Quantity <= 0))
                throw new ArgumentException("Each BOM edge must have a positive quantity.", nameof(input));
            if (HasDuplicateOrderingIdentity(edges))
                throw new ArgumentException("BOM edge ordering identities must be unique.", nameof(input));
            if (HasCycle(input.RootNodeId, edges))
                throw new ArgumentException("Clone BOM contains a cycle.", nameof(input));

            return nodes;
        }

        private static bool HasDuplicateOrderingIdentity(IEnumerable<PdmCloneBomEdge> edges)
        {
            return edges
                .GroupBy(edge => edge.ParentNodeId, StringComparer.OrdinalIgnoreCase)
                .Any(parent => parent
                    .GroupBy(edge => edge.SortOrder)
                    .Any(sortOrder => sortOrder
                        .GroupBy(edge => edge.ChildNodeId, StringComparer.OrdinalIgnoreCase)
                        .Any(child => child.Count() > 1)));
        }

        private static void ValidateNativeFileName(string nativeFileName)
        {
            if (string.IsNullOrWhiteSpace(nativeFileName) || Path.IsPathRooted(nativeFileName) ||
                nativeFileName.IndexOf('/') >= 0 || nativeFileName.IndexOf('\\') >= 0 ||
                string.Equals(nativeFileName, ".", StringComparison.Ordinal) || string.Equals(nativeFileName, "..", StringComparison.Ordinal))
                throw new ArgumentException("NativeFileName must be a file name under cad.", nameof(nativeFileName));
        }

        private static bool HasCycle(string rootNodeId, IEnumerable<PdmCloneBomEdge> edges)
        {
            var children = edges.GroupBy(edge => edge.ParentNodeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(edge => edge.ChildNodeId).ToArray(), StringComparer.OrdinalIgnoreCase);
            return HasCycle(rootNodeId, children, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private static bool HasCycle(string nodeId, IDictionary<string, string[]> children, ISet<string> active, ISet<string> complete)
        {
            if (!active.Add(nodeId)) return true;
            if (children.TryGetValue(nodeId, out var childIds))
            {
                foreach (var childId in childIds)
                {
                    if (!complete.Contains(childId) && HasCycle(childId, children, active, complete))
                        return true;
                }
            }
            active.Remove(nodeId);
            complete.Add(nodeId);
            return false;
        }

        private static IList<CloneOccurrence> BuildOccurrences(string rootNodeId, IEnumerable<PdmCloneBomEdge> orderedEdges)
        {
            var children = orderedEdges
                .GroupBy(edge => edge.ParentNodeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(edge => edge.SortOrder).ThenBy(edge => edge.ChildNodeId, StringComparer.OrdinalIgnoreCase).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var result = new List<CloneOccurrence>();
            AddOccurrence(rootNodeId, "0", null, null, children, result);
            return result;
        }

        private static void AddOccurrence(
            string nodeId,
            string occurrencePath,
            string parentOccurrenceId,
            PdmCloneBomEdge sourceEdge,
            IDictionary<string, PdmCloneBomEdge[]> children,
            IList<CloneOccurrence> result)
        {
            var occurrence = new PdmManifestOccurrence
            {
                OccurrenceId = occurrencePath,
                OccurrencePath = occurrencePath,
                ParentOccurrenceId = parentOccurrenceId,
                DefinitionId = nodeId,
                FindNumber = sourceEdge == null ? 0 : sourceEdge.SortOrder
            };
            result.Add(new CloneOccurrence { Manifest = occurrence, NodeId = nodeId, SourceEdge = sourceEdge });

            if (!children.TryGetValue(nodeId, out var childEdges)) return;
            for (var index = 0; index < childEdges.Length; index++)
            {
                var childEdge = childEdges[index];
                AddOccurrence(childEdge.ChildNodeId, occurrencePath + "/" + index, occurrence.OccurrenceId, childEdge, children, result);
            }
        }

        private static PdmManifestDefinition ToDefinition(PdmCloneNode node, string packageRevision)
        {
            return new PdmManifestDefinition
            {
                DefinitionId = node.NodeId,
                NodeId = node.NodeId,
                ItemCode = node.ItemCode,
                ItemType = node.ItemType,
                DisplayName = node.DisplayName,
                Revision = string.IsNullOrWhiteSpace(node.Revision) ? packageRevision : node.Revision,
                FileName = ToCadRelativePath(node.NativeFileName)
            };
        }

        private static IEnumerable<PdmManifestBomV2> ToBomEdges(IEnumerable<CloneOccurrence> occurrences)
        {
            return occurrences
                .Where(occurrence => occurrence.SourceEdge != null)
                .Select(occurrence => new PdmManifestBomV2
                {
                    ParentOccurrenceId = occurrence.Manifest.ParentOccurrenceId,
                    ChildDefinitionId = occurrence.NodeId,
                    Quantity = occurrence.SourceEdge.Quantity,
                    QuantityStatus = "IdentityUnavailable"
                })
                .ToArray();
        }

        private static string ToCadRelativePath(string nativeFileName)
        {
            return "cad/" + nativeFileName;
        }

        private static void WriteBranches(string packageRoot, string branchName)
        {
            var branches = new WorkspaceBranchRegistry();
            branches.Branches.Add(new WorkspaceBranch { Name = "main", CreatedAt = DateTime.UtcNow });
            if (!string.Equals(branchName, "main", StringComparison.OrdinalIgnoreCase))
                branches.Branches.Add(new WorkspaceBranch { Name = branchName, CreatedAt = DateTime.UtcNow });
            new WorkspaceService(new WorkspaceOptions()).SaveBranchRegistry(packageRoot, branches);
        }

        private static void RemoveCreatedArtifacts(
            string manifestPath,
            bool manifestCreated,
            string branchRegistryPath,
            bool branchRegistryCreated,
            string metadataDirectory,
            bool metadataDirectoryCreated)
        {
            if (manifestCreated && File.Exists(manifestPath))
                File.Delete(manifestPath);
            if (branchRegistryCreated && File.Exists(branchRegistryPath))
                File.Delete(branchRegistryPath);
            if (metadataDirectoryCreated && Directory.Exists(metadataDirectory) &&
                !Directory.EnumerateFileSystemEntries(metadataDirectory).Any())
                Directory.Delete(metadataDirectory);
        }

        private sealed class CloneOccurrence
        {
            public PdmManifestOccurrence Manifest { get; set; }
            public string NodeId { get; set; }
            public PdmCloneBomEdge SourceEdge { get; set; }
        }
    }
}
