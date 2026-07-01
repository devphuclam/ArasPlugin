using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace IdeaCadConnector.Workspace
{
    public sealed class PdmNamingPolicy
    {
        public string PolicyVersion { get; set; } = "aras01-draft-1";

        public string DetailPattern { get; set; } =
            @"^(?<project>[A-Za-z0-9][A-Za-z0-9 -]*)_Ver(?<version>\d+\.\d+)_(?<sequence>\d{3})$";

        public string AssemblyPattern { get; set; } =
            @"^Assembly-(?<project>.+?)-Ver(?<version>\d+\.\d+)(?<revision>[A-Za-z])(?:-(?<date>\d{6})-(?<run>\d+))?$";

        public string[] TrackedExtensions { get; set; } = { ".dwg", ".ics" };

        public string[] DocumentExtensions { get; set; } = { ".pdf" };

        public string[] IgnoredExtensions { get; set; } = { ".bak", ".tmp", ".lck", ".txt" };

        public string ProjectSeparator { get; set; } = "-";

        public string DetailDisplayPrefix { get; set; } = "Detail";
    }

    public sealed class PdmFolderAnalysis
    {
        public string FolderPath { get; set; }

        public string ProjectCode { get; set; }

        public string Version { get; set; }

        public PdmParsedFile PrimaryAssembly { get; set; }

        public IList<PdmParsedFile> TrackedFiles { get; } = new List<PdmParsedFile>();

        public IList<PdmParsedFile> DetailFiles { get; } = new List<PdmParsedFile>();

        public IList<PdmParsedFile> AssemblyFiles { get; } = new List<PdmParsedFile>();

        public IList<PdmParsedFile> DocumentFiles { get; } = new List<PdmParsedFile>();

        public IList<PdmParsedFile> IgnoredFiles { get; } = new List<PdmParsedFile>();

        public IList<PdmNamingIssue> Issues { get; } = new List<PdmNamingIssue>();

        public bool IsValid => Issues.All(issue => !issue.BlocksPush);
    }

    public sealed class PdmParsedFile
    {
        public string FullPath { get; set; }

        public string RelativePath { get; set; }

        public string FileName { get; set; }

        public string ProjectCode { get; set; }

        public string Version { get; set; }

        public string Revision { get; set; }

        public string DateToken { get; set; }

        public int? RunNumber { get; set; }

        public int? Sequence { get; set; }

        public string NodeType { get; set; }

        public string LogicalPartCode { get; set; }

        public string DisplayName { get; set; }

        public long Size { get; set; }

        public DateTime LastWriteTime { get; set; }

        public bool IsIgnored { get; set; }

        public string Status { get; set; }
    }

    public sealed class PdmNamingIssue
    {
        public string FileName { get; set; }

        public string Message { get; set; }

        public bool BlocksPush { get; set; }
    }

    public sealed class PdmBusinessStructureAnalysis
    {
        public string FolderPath { get; set; }

        public string ProjectCode { get; set; }

        public string RootDrawingFileName { get; set; }

        public IList<PdmBusinessNode> RootNodes { get; } = new List<PdmBusinessNode>();

        public IList<PdmNamingIssue> Issues { get; } = new List<PdmNamingIssue>();

        public bool HasStructure => RootNodes.Count > 0;
    }

    public sealed class PdmBusinessNode
    {
        public string Code { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string NodeType { get; set; }

        public string SourceFileName { get; set; }

        public IList<PdmBusinessNode> Children { get; } = new List<PdmBusinessNode>();
    }

    public sealed class StudyCase0603StructureParser
    {
        private static readonly Regex GroupRegex = new Regex(
            @"^(?<group>\d{2})\. (?<name>.+)\.pdf$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ChildRegex = new Regex(
            @"^(?<group>\d{2})(?<suffix>[A-Z])\. (?<parent>[^_]+)_(?<index>\d{2})_(?<name>.+)\.pdf$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RootDrawingRegex = new Regex(
            @"^(?<project>[A-Za-z0-9][A-Za-z0-9 -]*)_Ver(?<version>\d+\.\d+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public PdmBusinessStructureAnalysis Analyze(string folderPath, string projectCode)
        {
            var result = new PdmBusinessStructureAnalysis
            {
                FolderPath = folderPath,
                ProjectCode = projectCode
            };

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return result;
            }

            var files = new DirectoryInfo(folderPath)
                .GetFiles("*", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var groups = new Dictionary<string, PdmBusinessNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                if (file.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var childMatch = ChildRegex.Match(file.Name);
                    if (childMatch.Success)
                    {
                        var groupCode = childMatch.Groups["group"].Value;
                        var childCode = childMatch.Groups["index"].Value;
                        var childName = childMatch.Groups["name"].Value.Trim();

                        if (!groups.TryGetValue(groupCode, out var parentNode))
                        {
                            result.Issues.Add(new PdmNamingIssue
                            {
                                FileName = file.Name,
                                Message = "Child package exists without its top-level group PDF.",
                                BlocksPush = false
                            });
                            continue;
                        }

                        parentNode.Children.Add(new PdmBusinessNode
                        {
                            Code = groupCode + "-" + childCode,
                            Name = childName,
                            DisplayName = childCode + " " + childName,
                            NodeType = "Component",
                            SourceFileName = file.Name
                        });

                        continue;
                    }

                    var groupMatch = GroupRegex.Match(file.Name);
                    if (groupMatch.Success)
                    {
                        var groupCode = groupMatch.Groups["group"].Value;
                        var groupName = groupMatch.Groups["name"].Value.Trim();
                        var groupNode = new PdmBusinessNode
                        {
                            Code = groupCode,
                            Name = groupName,
                            DisplayName = groupCode + " " + groupName,
                            NodeType = "Assembly",
                            SourceFileName = file.Name
                        };

                        groups[groupCode] = groupNode;
                        result.RootNodes.Add(groupNode);
                    }
                }
                else if (file.Extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase))
                {
                    var rootMatch = RootDrawingRegex.Match(Path.GetFileNameWithoutExtension(file.Name));
                    if (rootMatch.Success)
                    {
                        result.RootDrawingFileName = file.Name;
                        if (string.IsNullOrWhiteSpace(result.ProjectCode))
                        {
                            result.ProjectCode = Aras01FolderAnalyzer.NormalizeProjectCode(rootMatch.Groups["project"].Value, "-");
                        }
                    }
                }
            }

            foreach (var group in result.RootNodes)
            {
                var orderedChildren = group.Children
                    .OrderBy(child => child.Code, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                group.Children.Clear();
                foreach (var child in orderedChildren)
                {
                    group.Children.Add(child);
                }
            }

            var orderedGroups = result.RootNodes
                .OrderBy(node => node.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.RootNodes.Clear();
            foreach (var node in orderedGroups)
            {
                result.RootNodes.Add(node);
            }

            return result;
        }
    }

    public sealed class Aras01FolderAnalyzer
    {
        private readonly PdmNamingPolicy _policy;
        private readonly Regex _detailRegex;
        private readonly Regex _assemblyRegex;
        private static readonly Regex RootDrawingRefRegex = new Regex(
            @"^(?<project>[A-Za-z0-9][A-Za-z0-9 -]*)_Ver(?<version>\d+\.\d+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Aras01FolderAnalyzer(PdmNamingPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _detailRegex = new Regex(_policy.DetailPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _assemblyRegex = new Regex(_policy.AssemblyPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public PdmFolderAnalysis Analyze(string folderPath)
        {
            var result = new PdmFolderAnalysis { FolderPath = folderPath };

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                result.Issues.Add(new PdmNamingIssue
                {
                    FileName = folderPath ?? string.Empty,
                    Message = "Folder does not exist.",
                    BlocksPush = true
                });
                return result;
            }

            foreach (var path in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                .Where(value => !IsInsideIgnoredWorkspaceFolder(folderPath, value))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                var fileInfo = new FileInfo(path);
                var extension = fileInfo.Extension.ToLowerInvariant();
                var parsed = CreateBaseFile(folderPath, fileInfo);

                if (Contains(_policy.IgnoredExtensions, extension))
                {
                    parsed.IsIgnored = true;
                    parsed.Status = "Ignored";
                    result.IgnoredFiles.Add(parsed);
                    continue;
                }

                if (Contains(_policy.DocumentExtensions, extension))
                {
                    parsed.Status = "Document";
                    result.DocumentFiles.Add(parsed);
                    continue;
                }

                if (!Contains(_policy.TrackedExtensions, extension))
                {
                    parsed.Status = "Unrecognized";
                    result.Issues.Add(new PdmNamingIssue
                    {
                        FileName = parsed.RelativePath,
                        Message = "Extension is not tracked by the active naming policy.",
                        BlocksPush = true
                    });
                    continue;
                }

                var stem = Path.GetFileNameWithoutExtension(fileInfo.Name);
                var assemblyMatch = _assemblyRegex.Match(stem);
                var detailMatch = _detailRegex.Match(stem);

                if (assemblyMatch.Success)
                {
                    ParseAssembly(parsed, assemblyMatch);
                    result.AssemblyFiles.Add(parsed);
                    result.TrackedFiles.Add(parsed);
                    continue;
                }

                if (detailMatch.Success)
                {
                    ParseDetail(parsed, detailMatch);
                    result.DetailFiles.Add(parsed);
                    result.TrackedFiles.Add(parsed);
                    continue;
                }

                if (extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase))
                {
                    var rootMatch = RootDrawingRefRegex.Match(stem);
                    if (rootMatch.Success)
                    {
                        parsed.ProjectCode = NormalizeProjectCode(rootMatch.Groups["project"].Value, _policy.ProjectSeparator);
                        parsed.Version = rootMatch.Groups["version"].Value;
                        parsed.NodeType = "Reference";
                        parsed.Status = "Drawing";
                        result.DocumentFiles.Add(parsed);
                        continue;
                    }
                }

                parsed.Status = "Invalid name";
                result.Issues.Add(new PdmNamingIssue
                {
                    FileName = parsed.RelativePath,
                    Message = "Filename does not match Assembly or Detail rule.",
                    BlocksPush = true
                });
            }

            ResolveProjectIdentity(result);
            ResolvePrimaryAssembly(result);
            ValidateSequences(result);
            return result;
        }

        private static bool IsInsideIgnoredWorkspaceFolder(string rootFolder, string filePath)
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var relativePath = filePath.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase)
                ? filePath.Substring(rootFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : filePath;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Any(segment =>
                segment.Equals(".idea-pdm", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals(".vs", StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeProjectCode(string value, string separator)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = Regex.Replace(value.Trim(), @"[\s_-]+", separator ?? "-");
            return normalized.Trim((separator ?? "-").ToCharArray()).ToUpperInvariant();
        }

        private void ParseAssembly(PdmParsedFile parsed, Match match)
        {
            parsed.ProjectCode = NormalizeProjectCode(match.Groups["project"].Value, _policy.ProjectSeparator);
            parsed.Version = match.Groups["version"].Value;
            parsed.Revision = match.Groups["revision"].Value.ToUpperInvariant();
            parsed.DateToken = match.Groups["date"].Success ? match.Groups["date"].Value : null;
            parsed.RunNumber = match.Groups["run"].Success
                ? int.Parse(match.Groups["run"].Value, CultureInfo.InvariantCulture)
                : (int?)null;
            parsed.NodeType = "Assembly";
            parsed.LogicalPartCode = parsed.ProjectCode;
            parsed.DisplayName = parsed.ProjectCode;
            parsed.Status = "Tracked";
        }

        private void ParseDetail(PdmParsedFile parsed, Match match)
        {
            parsed.ProjectCode = NormalizeProjectCode(match.Groups["project"].Value, _policy.ProjectSeparator);
            parsed.Version = match.Groups["version"].Value;
            parsed.Sequence = int.Parse(match.Groups["sequence"].Value, CultureInfo.InvariantCulture);
            parsed.NodeType = "Component";
            parsed.LogicalPartCode = parsed.ProjectCode + "-" + parsed.Sequence.Value.ToString("000", CultureInfo.InvariantCulture);
            parsed.DisplayName = _policy.DetailDisplayPrefix + " " +
                parsed.Sequence.Value.ToString("000", CultureInfo.InvariantCulture);
            parsed.Status = "Tracked";
        }

        private static PdmParsedFile CreateBaseFile(string root, FileInfo fileInfo)
        {
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var relativePath = fileInfo.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? fileInfo.FullName.Substring(prefix.Length)
                : fileInfo.Name;

            return new PdmParsedFile
            {
                FullPath = fileInfo.FullName,
                RelativePath = relativePath,
                FileName = fileInfo.Name,
                Size = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTime
            };
        }

        private static bool Contains(IEnumerable<string> values, string candidate)
        {
            return values != null && values.Any(value =>
                string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
        }

        private static void ResolveProjectIdentity(PdmFolderAnalysis result)
        {
            var projectCodes = result.TrackedFiles
                .Select(file => file.ProjectCode)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (projectCodes.Count == 1)
            {
                result.ProjectCode = projectCodes[0];
            }
            else if (projectCodes.Count > 1)
            {
                result.Issues.Add(new PdmNamingIssue
                {
                    FileName = result.FolderPath,
                    Message = "Folder contains multiple normalized project codes: " +
                        string.Join(", ", projectCodes),
                    BlocksPush = true
                });
            }

            var versions = result.TrackedFiles
                .Select(file => file.Version)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (versions.Count == 1)
            {
                result.Version = versions[0];
            }
            else if (versions.Count > 1)
            {
                result.Issues.Add(new PdmNamingIssue
                {
                    FileName = result.FolderPath,
                    Message = "Folder contains multiple design versions: " + string.Join(", ", versions),
                    BlocksPush = true
                });
            }
        }

        private static void ResolvePrimaryAssembly(PdmFolderAnalysis result)
        {
            result.PrimaryAssembly = result.AssemblyFiles
                .OrderByDescending(file => file.Revision, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(file => file.DateToken, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(file => file.RunNumber ?? 0)
                .ThenByDescending(file => file.LastWriteTime)
                .FirstOrDefault();

            if (result.PrimaryAssembly == null && result.DetailFiles.Count > 0)
            {
                result.Issues.Add(new PdmNamingIssue
                {
                    FileName = result.FolderPath,
                    Message = "No Assembly file was found for the detail files.",
                    BlocksPush = true
                });
            }
        }

        private static void ValidateSequences(PdmFolderAnalysis result)
        {
            var duplicateGroups = result.DetailFiles
                .Where(file => file.Sequence.HasValue)
                .GroupBy(file => file.Sequence.Value)
                .Where(group => group.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                result.Issues.Add(new PdmNamingIssue
                {
                    FileName = string.Join(", ", group.Select(file => file.FileName)),
                    Message = "Duplicate detail sequence " + group.Key.ToString("000", CultureInfo.InvariantCulture) + ".",
                    BlocksPush = true
                });
            }
        }
    }
}
