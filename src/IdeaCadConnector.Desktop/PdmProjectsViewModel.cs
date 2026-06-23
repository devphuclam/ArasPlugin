using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Workspace;
using Newtonsoft.Json;
using WinForms = System.Windows.Forms;

namespace IdeaCadConnector.Desktop
{
    public sealed class PdmProjectsViewModel : INotifyPropertyChanged
    {
        private readonly RelayCommand _pushCommand;
        private readonly RelayCommand _analyzeFolderCommand;
        private readonly RelayCommand _browseFolderCommand;
        private readonly RelayCommand _refreshCommand;
        private readonly Dictionary<string, List<PdmDocumentItem>> _documentsByPartCode = new Dictionary<string, List<PdmDocumentItem>>(StringComparer.OrdinalIgnoreCase);
        private PdmFolderAnalysis _latestAnalysis;
        private PdmBusinessStructureAnalysis _latestBusinessStructure;
        private PdmStructureNode _selectedNode;
        private string _selectedRepository;
        private string _selectedBranch;
        private string _selectedCommit;
        private string _statusMessage;
        private string _folderPath;
        private string _namingPolicyVersion;
        private string _analysisSummary;
        private int _totalChangeCount;
        private int _trackedFileCount;
        private int _ignoredFileCount;
        private int _blockingIssueCount;
        private string _connectionDisplayName;
        private string _connectionDatabase;
        private bool _isAnalyzing;
        private bool _isPushing;
        private PushPreview _pushPreview;
        private string _commitMessage;

        public PdmProjectsViewModel()
        {
            Repositories = new ObservableCollection<string>();
            Branches = new ObservableCollection<string> { "main", "experiment" };
            Commits = new ObservableCollection<string>
            {
                "Working folder analysis",
                "C1 - Initial project import"
            };
            Structure = new ObservableCollection<PdmStructureNode>();
            Changes = new ObservableCollection<PdmFileChange>();
            Documents = new ObservableCollection<PdmDocumentItem>();
            NamingPreview = new ObservableCollection<PdmNamingPreviewItem>();
            ProjectFiles = new ObservableCollection<PdmProjectFileNode>();
            PreviewParts = new ObservableCollection<PartPreviewRow>();
            PreviewCads = new ObservableCollection<CadPreviewRow>();
            PreviewDocuments = new ObservableCollection<DocumentPreviewRow>();
            PreviewIgnoredFiles = new ObservableCollection<IgnoredPreviewRow>();

            SelectedBranch = Branches[0];
            SelectedCommit = Commits[0];
            FolderPath = GetDefaultSampleFolder();
            AnalysisSummary = "Select a project folder to preview the product structure.";
            StatusMessage = "PDM repository preview is ready.";
            RefreshConnectionStatus();

            CloneCommand = new RelayCommand(_ => StatusMessage = "Clone will use the selected repository and commit.");
            PullCommand = new RelayCommand(_ => StatusMessage = "Pull is not connected to Aras yet.");
            _pushCommand = new RelayCommand(_ => PushWorkspace(), _ => !IsPushing && CanPush);
            PushCommand = _pushCommand;
            NewBranchCommand = new RelayCommand(_ => StatusMessage = "New branch will start from the selected commit.");
            _refreshCommand = new RelayCommand(_ => AnalyzeFolder());
            RefreshCommand = _refreshCommand;
            _analyzeFolderCommand = new RelayCommand(_ => AnalyzeFolder());
            AnalyzeFolderCommand = _analyzeFolderCommand;
            _browseFolderCommand = new RelayCommand(_ => BrowseFolder());
            BrowseFolderCommand = _browseFolderCommand;
            OpenDocumentCommand = new RelayCommand(doc => OpenDocument(doc as PdmDocumentItem), doc => doc is PdmDocumentItem);

            AnalyzeFolder();
        }

        public ObservableCollection<string> Repositories { get; }
        public ObservableCollection<string> Branches { get; }
        public ObservableCollection<string> Commits { get; }
        public ObservableCollection<PdmStructureNode> Structure { get; }
        public ObservableCollection<PdmFileChange> Changes { get; }
        public ObservableCollection<PdmDocumentItem> Documents { get; }
        public ObservableCollection<PdmNamingPreviewItem> NamingPreview { get; }
        public ObservableCollection<PdmProjectFileNode> ProjectFiles { get; }
        public ObservableCollection<PartPreviewRow> PreviewParts { get; }
        public ObservableCollection<CadPreviewRow> PreviewCads { get; }
        public ObservableCollection<DocumentPreviewRow> PreviewDocuments { get; }
        public ObservableCollection<IgnoredPreviewRow> PreviewIgnoredFiles { get; }

        public string SelectedRepository
        {
            get => _selectedRepository;
            set => SetField(ref _selectedRepository, value);
        }

        public string SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (SetField(ref _selectedBranch, value))
                {
                    OnPropertyChanged(nameof(IsMainBranch));
                }
            }
        }

        public string SelectedCommit
        {
            get => _selectedCommit;
            set => SetField(ref _selectedCommit, value);
        }

        public string FolderPath
        {
            get => _folderPath;
            set => SetField(ref _folderPath, value);
        }

        public string NamingPolicyVersion
        {
            get => _namingPolicyVersion;
            set => SetField(ref _namingPolicyVersion, value);
        }

        public string AnalysisSummary
        {
            get => _analysisSummary;
            set => SetField(ref _analysisSummary, value);
        }

        public int TotalChangeCount
        {
            get => _totalChangeCount;
            set => SetField(ref _totalChangeCount, value);
        }

        public int TrackedFileCount
        {
            get => _trackedFileCount;
            set => SetField(ref _trackedFileCount, value);
        }

        public int IgnoredFileCount
        {
            get => _ignoredFileCount;
            set => SetField(ref _ignoredFileCount, value);
        }

        public int BlockingIssueCount
        {
            get => _blockingIssueCount;
            set
            {
                if (SetField(ref _blockingIssueCount, value))
                {
                    OnPropertyChanged(nameof(HasBlockingIssues));
                    OnPropertyChanged(nameof(CanPush));
                }
            }
        }

        public bool IsMainBranch => string.Equals(SelectedBranch, "main", StringComparison.OrdinalIgnoreCase);

        public bool HasBlockingIssues => BlockingIssueCount > 0;

        public bool CanPush =>
            !IsPushing &&
            (_pushPreview?.Readiness?.CanPush ?? false) &&
            MainViewModel.SharedPdmClient != null;

        public bool HasStructure => Structure.Count > 0;
        public bool HasProjectFiles => ProjectFiles.Count > 0;
        public bool HasChanges => Changes.Count > 0;
        public bool HasNamingPreview => NamingPreview.Count > 0;
        public bool HasDocuments => Documents.Count > 0;
        public bool HasSelectedNode => SelectedNode != null;

        public PdmStructureNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetField(ref _selectedNode, value))
                {
                    OnPropertyChanged(nameof(HasSelectedNode));
                    RefreshSelectedDocuments();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public string ConnectionDisplayName
        {
            get => _connectionDisplayName;
            set => SetField(ref _connectionDisplayName, value);
        }

        public string ConnectionDatabase
        {
            get => _connectionDatabase;
            set => SetField(ref _connectionDatabase, value);
        }

        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set => SetField(ref _isAnalyzing, value);
        }

        public bool IsPushing
        {
            get => _isPushing;
            set
            {
                if (SetField(ref _isPushing, value))
                {
                    _pushCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string CommitMessage
        {
            get => _commitMessage;
            set => SetField(ref _commitMessage, value);
        }

        public PushReadiness PushPreviewReadiness => _pushPreview?.Readiness;

        public bool HasPushPreview => _pushPreview != null;

        public string WorkingTreeSummary
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                var added = 0; var modified = 0; var renamed = 0; var deleted = 0;
                foreach (var c in Changes)
                {
                    if (c.ChangeType == "Assembly" || c.ChangeType == "Component")
                        added++;
                }
                if (added > 0) parts.Add($"{added} new");
                if (modified > 0) parts.Add($"{modified} modified");
                if (renamed > 0) parts.Add($"{renamed} renamed");
                if (deleted > 0) parts.Add($"{deleted} deleted");
                return parts.Count > 0 ? string.Join(", ", parts) : "No changes yet";
            }
        }

        public ICommand CloneCommand { get; }
        public ICommand PullCommand { get; }
        public ICommand PushCommand { get; }
        public ICommand NewBranchCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AnalyzeFolderCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand OpenDocumentCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void BrowseFolder()
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select a project folder to analyze with the active naming policy.";
                dialog.ShowNewFolderButton = false;
                dialog.SelectedPath = Directory.Exists(FolderPath) ? FolderPath : GetDefaultSampleFolder();

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    FolderPath = dialog.SelectedPath;
                    AnalyzeFolder();
                }
            }
        }

        private async void PushWorkspace()
        {
            RefreshConnectionStatus();

            if (!CanPush)
            {
                StatusMessage = "Push is blocked until the folder passes naming validation and you are connected to Aras.";
                return;
            }

            var client = MainViewModel.SharedPdmClient;
            if (client == null)
            {
                StatusMessage = "Not connected to Aras. Sign in first.";
                return;
            }

            IsPushing = true;
            StatusMessage = "Pushing to Aras...";

            try
            {
                var request = BuildPushRequest();
                var result = await client.PushAsync(request, CancellationToken.None);

                if (result.Success)
                {
                    var msg = string.Format(
                        "Push complete. Created {0} part(s), {1} CAD(s), {2} document(s). Commit: {3}",
                        result.PartResults?.Count(r => r.Success) ?? 0,
                        result.CadResults?.Count(r => r.Success) ?? 0,
                        result.DocumentResults?.Count(r => r.Success) ?? 0,
                        result.CommitId ?? "-");

                    if (result.Warnings?.Count > 0)
                    {
                        msg += " Warning: " + string.Join("; ", result.Warnings);
                    }

                    StatusMessage = msg;

                    UpdatePreviewResults(result);
                }
                else
                {
                    StatusMessage = "Push failed: " + (result.ErrorMessage ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Push failed: " + ex.Message;
            }
            finally
            {
                IsPushing = false;
            }
        }

        private PdmPushRequest BuildPushRequest()
        {
            if (_pushPreview == null)
                return null;

            return new PdmPushRequest
            {
                RepositoryCode = _pushPreview.RepositoryCode,
                ProjectName = _pushPreview.ProjectName,
                TargetBranch = SelectedBranch ?? "main",
                CommitMessage = CommitMessage ?? string.Empty,
                PackageSourcePath = FolderPath,
                CadSourcePath = _latestAnalysis?.FolderPath,
                Parts = _pushPreview.Parts.Select(p => new PdmPartRequest
                {
                    LogicalCode = p.LogicalCode,
                    ParentLogicalCode = p.ParentLogicalCode,
                    PartNumber = p.PartNumber,
                    Name = p.Name,
                    Classification = p.Classification,
                    Quantity = p.Quantity
                }).ToList(),
                Cads = _pushPreview.Cads.Select(c => new PdmCadRequest
                {
                    SourceFileName = c.SourceFileName,
                    LogicalCode = c.LogicalCode,
                    CadNumber = c.CadNumber,
                    Classification = c.Classification,
                    LinkedPartLogicalCode = c.LinkedPartLogicalCode ?? c.LogicalCode
                }).ToList(),
                Documents = _pushPreview.Documents.Select(d => new PdmDocumentRequest
                {
                    SourceFileName = d.SourceFileName,
                    LogicalCode = d.LogicalCode,
                    DocumentNumber = d.DocumentNumber,
                    Classification = d.Classification,
                    LinkTargetType = d.LinkTargetType,
                    LinkedPartLogicalCode = d.LinkedPartLogicalCode ?? d.LogicalCode
                }).ToList()
            };
        }

        private void UpdatePreviewResults(PdmPushResult result)
        {
            if (result.PartResults != null)
            {
                for (int i = 0; i < result.PartResults.Count && i < PreviewParts.Count; i++)
                {
                    var res = result.PartResults[i];
                    var row = PreviewParts[i];
                    if (res.Success)
                        row.Action = "Created";
                    else
                        row.Action = "Failed: " + (res.ErrorMessage ?? "Unknown");
                }
            }

            if (result.CadResults != null)
            {
                for (int i = 0; i < result.CadResults.Count && i < PreviewCads.Count; i++)
                {
                    var res = result.CadResults[i];
                    var row = PreviewCads[i];
                    if (res.Success)
                        row.Action = "Created";
                    else
                        row.Action = "Failed: " + (res.ErrorMessage ?? "Unknown");
                }
            }

            if (result.DocumentResults != null)
            {
                for (int i = 0; i < result.DocumentResults.Count && i < PreviewDocuments.Count; i++)
                {
                    var res = result.DocumentResults[i];
                    var row = PreviewDocuments[i];
                    if (res.Success)
                        row.Action = "Created";
                    else
                        row.Action = "Failed: " + (res.ErrorMessage ?? "Unknown");
                }
            }
        }

        private void AnalyzeFolder()
        {
            IsAnalyzing = true;
            var policy = LoadPolicy();
            NamingPolicyVersion = policy.PolicyVersion;

            var sources = ResolveAnalysisSources(FolderPath);
            if (!string.IsNullOrWhiteSpace(sources.CadFolder) && Directory.Exists(sources.CadFolder))
            {
                _latestAnalysis = new Aras01FolderAnalyzer(policy).Analyze(sources.CadFolder);
            }
            else
            {
                _latestAnalysis = new PdmFolderAnalysis
                {
                    FolderPath = FolderPath
                };
            }

            _latestBusinessStructure = new StudyCase0603StructureParser().Analyze(
                sources.PackageFolder,
                _latestAnalysis.ProjectCode);

            if (string.IsNullOrWhiteSpace(_latestAnalysis.ProjectCode) &&
                !string.IsNullOrWhiteSpace(_latestBusinessStructure?.ProjectCode))
            {
                _latestAnalysis.ProjectCode = _latestBusinessStructure.ProjectCode;
            }

            if (_latestAnalysis.PrimaryAssembly == null &&
                !string.IsNullOrWhiteSpace(_latestBusinessStructure?.RootDrawingFileName))
            {
                var rootDrawing = new PdmParsedFile
                {
                    FileName = _latestBusinessStructure.RootDrawingFileName,
                    RelativePath = _latestBusinessStructure.RootDrawingFileName,
                    FullPath = sources.PackageFolder == null
                        ? _latestBusinessStructure.RootDrawingFileName
                        : Path.Combine(sources.PackageFolder, _latestBusinessStructure.RootDrawingFileName),
                    ProjectCode = _latestAnalysis.ProjectCode,
                    NodeType = "Assembly",
                    Status = "Package root drawing"
                };

                _latestAnalysis.PrimaryAssembly = rootDrawing;
                _latestAnalysis.TrackedFiles.Add(rootDrawing);
                _latestAnalysis.AssemblyFiles.Add(rootDrawing);
            }

            Structure.Clear();
            Changes.Clear();
            Documents.Clear();
            NamingPreview.Clear();
            Repositories.Clear();
            ProjectFiles.Clear();
            PreviewParts.Clear();
            PreviewCads.Clear();
            PreviewDocuments.Clear();
            PreviewIgnoredFiles.Clear();
            _documentsByPartCode.Clear();
            _pushPreview = null;
            SelectedNode = null;

            TrackedFileCount = _latestAnalysis.TrackedFiles.Count;
            IgnoredFileCount = _latestAnalysis.IgnoredFiles.Count;
            BlockingIssueCount = _latestAnalysis.Issues.Count(issue => issue.BlocksPush);
            TotalChangeCount = TrackedFileCount + IgnoredFileCount + BlockingIssueCount;

            if (!string.IsNullOrWhiteSpace(_latestAnalysis.ProjectCode))
            {
                Repositories.Add(_latestAnalysis.ProjectCode);
                SelectedRepository = _latestAnalysis.ProjectCode;
            }
            else
            {
                SelectedRepository = null;
            }

            BuildNamingPreview(_latestAnalysis);
            BuildChanges(_latestAnalysis);
            BuildStructure(_latestAnalysis, _latestBusinessStructure);
            BuildDocuments(_latestAnalysis, _latestBusinessStructure, sources.PackageFolder ?? FolderPath, sources.CadFolder ?? FolderPath);
            BuildProjectFiles(sources);
            BuildSummary(_latestAnalysis, _latestBusinessStructure, sources);
            BuildPushPreview(_latestAnalysis, _latestBusinessStructure);

            OnPropertyChanged(nameof(HasStructure));
            OnPropertyChanged(nameof(HasProjectFiles));
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(HasNamingPreview));
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(HasPushPreview));
            OnPropertyChanged(nameof(PushPreviewReadiness));
            OnPropertyChanged(nameof(CanPush));
            OnPropertyChanged(nameof(WorkingTreeSummary));

            _pushCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
            _analyzeFolderCommand.RaiseCanExecuteChanged();
            _browseFolderCommand.RaiseCanExecuteChanged();

            IsAnalyzing = false;
        }

        private void BuildNamingPreview(PdmFolderAnalysis analysis)
        {
            foreach (var file in analysis.TrackedFiles)
            {
                NamingPreview.Add(new PdmNamingPreviewItem(
                    file.FileName,
                    file.NodeType,
                    file.LogicalPartCode,
                    file.Version,
                    file.Revision ?? "-",
                    file.Sequence.HasValue ? file.Sequence.Value.ToString("000") : "-",
                    file.Status));
            }

            foreach (var file in analysis.IgnoredFiles)
            {
                NamingPreview.Add(new PdmNamingPreviewItem(
                    file.FileName,
                    "Ignored",
                    "-",
                    "-",
                    "-",
                    "-",
                    file.Status));
            }

            foreach (var issue in analysis.Issues)
            {
                NamingPreview.Add(new PdmNamingPreviewItem(
                    issue.FileName,
                    "Invalid",
                    "-",
                    "-",
                    "-",
                    "-",
                    issue.Message));
            }
        }

        private void BuildChanges(PdmFolderAnalysis analysis)
        {
            foreach (var assembly in analysis.AssemblyFiles.OrderBy(file => file.FileName))
            {
                Changes.Add(new PdmFileChange("Assembly", assembly.RelativePath, "#FF2967EF"));
            }

            foreach (var detail in analysis.DetailFiles.OrderBy(file => file.Sequence))
            {
                Changes.Add(new PdmFileChange("Component", detail.RelativePath, "#FF1F9D55"));
            }

            foreach (var issue in analysis.Issues)
            {
                Changes.Add(new PdmFileChange("Blocked", issue.FileName, "#FFD54B4B"));
            }
        }

        private void BuildStructure(PdmFolderAnalysis analysis, PdmBusinessStructureAnalysis businessStructure)
        {
            if (string.IsNullOrWhiteSpace(analysis.ProjectCode))
            {
                return;
            }

            var detailCadMap = BuildDetailCadMap(analysis, businessStructure);

            var root = new PdmStructureNode(
                analysis.ProjectCode,
                analysis.ProjectCode,
                "Assembly",
                1,
                analysis.PrimaryAssembly?.Revision ?? "-",
                businessStructure != null && businessStructure.HasStructure ? "Hybrid structure preview" : (analysis.IsValid ? "Ready to push" : "Fix naming"),
                "#FF7C47DC",
                primaryCad: analysis.PrimaryAssembly?.FileName ?? "-",
                sourceDocument: businessStructure?.RootDrawingFileName ?? analysis.PrimaryAssembly?.FileName ?? "-");

            if (businessStructure != null && businessStructure.HasStructure)
            {
                foreach (var groupNode in businessStructure.RootNodes)
                {
                    root.Children.Add(CreateBusinessStructureNode(
                        analysis.ProjectCode,
                        groupNode,
                        analysis.ProjectCode,
                        detailCadMap));
                }
            }
            else
            {
                foreach (var detail in analysis.DetailFiles.OrderBy(file => file.Sequence))
                {
                    root.Children.Add(new PdmStructureNode(
                        detail.DisplayName,
                        detail.LogicalPartCode,
                        "Component",
                        1,
                        detail.Version ?? "-",
                        "Parsed from name",
                        "#FF1F9D55",
                        primaryCad: detail.FileName,
                        sourceDocument: detail.FileName));
                }
            }

            Structure.Add(root);
            SelectedNode = root;
        }

        private PdmStructureNode CreateBusinessStructureNode(
            string projectCode,
            PdmBusinessNode businessNode,
            string parentCode,
            IDictionary<string, PdmParsedFile> detailCadMap)
        {
            var normalizedName = FormatBusinessNodeName(businessNode.Name);
            var logicalCode = string.IsNullOrWhiteSpace(parentCode)
                ? normalizedName
                : parentCode + "__" + normalizedName;
            var primaryCad = ResolvePrimaryCadForNode(businessNode, logicalCode, detailCadMap);

            var node = new PdmStructureNode(
                normalizedName,
                logicalCode,
                businessNode.NodeType,
                1,
                "-",
                "Package inferred",
                businessNode.NodeType == "Assembly" ? "#FF2967EF" : "#FF1F9D55",
                primaryCad: primaryCad,
                sourceDocument: businessNode.SourceFileName);

            foreach (var child in businessNode.Children)
            {
                node.Children.Add(CreateBusinessStructureNode(projectCode, child, logicalCode, detailCadMap));
            }

            return node;
        }

        private void BuildDocuments(PdmFolderAnalysis analysis, PdmBusinessStructureAnalysis businessStructure, string packageFolder, string cadFolder)
        {
            _documentsByPartCode.Clear();

            if (string.IsNullOrWhiteSpace(analysis.ProjectCode))
            {
                return;
            }

            AddDocument(analysis.ProjectCode, analysis.PrimaryAssembly?.FileName, "Primary CAD", ResolveFilePath(analysis.PrimaryAssembly?.FileName, packageFolder, cadFolder));

            if (!string.IsNullOrWhiteSpace(businessStructure?.RootDrawingFileName))
            {
                AddDocument(analysis.ProjectCode, businessStructure.RootDrawingFileName, "Root Drawing", ResolveFilePath(businessStructure.RootDrawingFileName, packageFolder, cadFolder));
            }

            foreach (var olderAssembly in analysis.AssemblyFiles
                .Where(file => !ReferenceEquals(file, analysis.PrimaryAssembly))
                .OrderBy(file => file.FileName))
            {
                AddDocument(analysis.ProjectCode, olderAssembly.FileName, "Assembly DWG REV " + olderAssembly.Revision, ResolveFilePath(olderAssembly.FileName, packageFolder, cadFolder));
            }

            if (businessStructure != null && businessStructure.HasStructure)
            {
                BuildBusinessDocuments(analysis.ProjectCode, businessStructure.RootNodes, analysis.ProjectCode, packageFolder);
            }

            RefreshSelectedDocuments();
        }

        private static string ResolveFilePath(string fileName, string packageFolder, string cadFolder)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (!string.IsNullOrWhiteSpace(packageFolder))
            {
                var path = System.IO.Path.Combine(packageFolder, fileName);
                if (System.IO.File.Exists(path))
                    return path;
            }

            if (!string.IsNullOrWhiteSpace(cadFolder))
            {
                var path = System.IO.Path.Combine(cadFolder, fileName);
                if (System.IO.File.Exists(path))
                    return path;
            }

            return null;
        }

        private void BuildBusinessDocuments(string projectCode, IEnumerable<PdmBusinessNode> nodes, string parentCode, string sourceFolder = null)
        {
            foreach (var node in nodes)
            {
                var normalizedName = FormatBusinessNodeName(node.Name);
                var logicalCode = string.IsNullOrWhiteSpace(parentCode)
                    ? normalizedName
                    : parentCode + "__" + normalizedName;

                var sourcePath = string.IsNullOrWhiteSpace(node.SourceFileName) || string.IsNullOrWhiteSpace(sourceFolder)
                    ? null
                    : System.IO.Path.Combine(sourceFolder, node.SourceFileName);

                AddDocument(logicalCode, node.SourceFileName, node.NodeType == "Assembly" ? "Package group" : "Package detail", sourcePath);

                foreach (var child in node.Children)
                {
                    BuildBusinessDocuments(projectCode, new[] { child }, logicalCode, sourceFolder);
                }
            }
        }

        private void RefreshSelectedDocuments()
        {
            Documents.Clear();

            if (SelectedNode == null || string.IsNullOrWhiteSpace(SelectedNode.PartCode))
            {
                return;
            }

            if (_documentsByPartCode.TryGetValue(SelectedNode.PartCode, out var selectedDocuments))
            {
                foreach (var document in selectedDocuments)
                {
                    Documents.Add(document);
                }
            }
        }

        private void OpenDocument(PdmDocumentItem document)
        {
            if (document?.CanOpen != true)
            {
                StatusMessage = document?.SourcePath == null
                    ? "Cannot open: file path is unknown."
                    : "Cannot open: file not found at " + document.SourcePath;
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(document.SourcePath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to open document: " + ex.Message;
            }
        }

        private void AddDocument(string partCode, string name, string kind, string sourcePath = null)
        {
            if (string.IsNullOrWhiteSpace(partCode) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (!_documentsByPartCode.TryGetValue(partCode, out var list))
            {
                list = new List<PdmDocumentItem>();
                _documentsByPartCode[partCode] = list;
            }

            if (list.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            list.Add(new PdmDocumentItem(name, kind, sourcePath));
        }

        private static Dictionary<string, PdmParsedFile> BuildDetailCadMap(
            PdmFolderAnalysis analysis,
            PdmBusinessStructureAnalysis businessStructure)
        {
            var map = new Dictionary<string, PdmParsedFile>(StringComparer.OrdinalIgnoreCase);
            if (businessStructure == null || !businessStructure.HasStructure)
            {
                return map;
            }

            var businessDetails = businessStructure.RootNodes
                .SelectMany(group => group.Children)
                .ToList();

            var detailFiles = analysis.DetailFiles
                .OrderBy(file => file.Sequence ?? int.MaxValue)
                .ToList();
            var detailBySequence = detailFiles
                .Where(file => file.Sequence.HasValue)
                .GroupBy(file => file.Sequence.Value)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var businessDetail in businessDetails)
            {
                if (string.IsNullOrWhiteSpace(businessDetail.SourceFileName))
                {
                    continue;
                }

                var sequence = ExtractBusinessSequence(businessDetail);
                if (!sequence.HasValue)
                {
                    continue;
                }

                if (detailBySequence.TryGetValue(sequence.Value, out var matchedDetail))
                {
                    map[businessDetail.SourceFileName] = matchedDetail;
                }
            }

            return map;
        }

        private static int? ExtractBusinessSequence(PdmBusinessNode businessNode)
        {
            if (businessNode == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(businessNode.Code))
            {
                var codeParts = businessNode.Code.Split('-');
                if (codeParts.Length > 1 &&
                    int.TryParse(codeParts[codeParts.Length - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var codeSequence))
                {
                    return codeSequence;
                }
            }

            if (!string.IsNullOrWhiteSpace(businessNode.DisplayName))
            {
                var prefix = businessNode.DisplayName
                    .Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (int.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var displaySequence))
                {
                    return displaySequence;
                }
            }

            if (!string.IsNullOrWhiteSpace(businessNode.SourceFileName))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(businessNode.SourceFileName);
                var tokens = fileNameWithoutExtension.Split('_');
                if (tokens.Length >= 2 &&
                    int.TryParse(tokens[tokens.Length - 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileSequence))
                {
                    return fileSequence;
                }
            }

            return null;
        }
        private static string ResolvePrimaryCadForNode(
            PdmBusinessNode businessNode,
            string logicalCode,
            IDictionary<string, PdmParsedFile> detailCadMap)
        {
            if (businessNode == null)
            {
                return "-";
            }

            if (businessNode.NodeType == "Component" &&
                !string.IsNullOrWhiteSpace(businessNode.SourceFileName) &&
                detailCadMap != null &&
                detailCadMap.TryGetValue(businessNode.SourceFileName, out var detailCad))
            {
                return detailCad.FileName;
            }

            return "-";
        }

        private void BuildProjectFiles(PdmAnalysisSources sources)
        {
            if (sources == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(sources.PackageFolder) && Directory.Exists(sources.PackageFolder))
            {
                ProjectFiles.Add(BuildFolderRootNode("Business packages", sources.PackageFolder));
            }

            if (!string.IsNullOrWhiteSpace(sources.CadFolder) && Directory.Exists(sources.CadFolder))
            {
                ProjectFiles.Add(BuildFolderRootNode("CAD source", sources.CadFolder));
            }

            if (ProjectFiles.Count == 0 && !string.IsNullOrWhiteSpace(sources.SelectedFolder))
            {
                ProjectFiles.Add(BuildFolderRootNode(
                    new DirectoryInfo(sources.SelectedFolder).Name,
                    sources.SelectedFolder));
            }
        }

        private PdmProjectFileNode BuildFolderRootNode(string rootName, string folderPath)
        {
            var root = new PdmProjectFileNode(rootName, folderPath, true, "Folder");
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return root;
            }

            foreach (var filePath in Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fileInfo = new FileInfo(filePath);
                root.Children.Add(new PdmProjectFileNode(
                    fileInfo.Name,
                    fileInfo.Name,
                    false,
                    ClassifySelectedFolderFile(fileInfo)));
            }

            return root;
        }

        private string ClassifySelectedFolderFile(FileInfo fileInfo)
        {
            if (fileInfo.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return "Package";
            }

            if (fileInfo.Extension.Equals(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                return "CAD";
            }

            if (fileInfo.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return "Spreadsheet";
            }

            if (fileInfo.Extension.Equals(".err", StringComparison.OrdinalIgnoreCase) ||
                fileInfo.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase))
            {
                return "Log";
            }

            if (fileInfo.Extension.Equals(".bak", StringComparison.OrdinalIgnoreCase))
            {
                return "Ignored";
            }

            return "File";
        }

        private void BuildSummary(PdmFolderAnalysis analysis, PdmBusinessStructureAnalysis businessStructure, PdmAnalysisSources sources)
        {
            if (!Directory.Exists(FolderPath))
            {
                AnalysisSummary = "Select an existing project folder before analyzing the naming policy.";
                StatusMessage = "Folder path is missing or invalid.";
                return;
            }

            if (analysis.IsValid)
            {
                if (businessStructure != null && businessStructure.HasStructure)
                {
                    var componentCount = businessStructure.RootNodes.Sum(node => node.Children.Count);
                    AnalysisSummary = string.Format(
                        "{0} is valid for naming policy {1}. Business structure shows {2} groups and {3} child components; CAD sequence comes from {4}.",
                        analysis.ProjectCode ?? "Folder",
                        NamingPolicyVersion,
                        businessStructure.RootNodes.Count,
                        componentCount,
                        Path.GetFileName(sources.CadFolder ?? FolderPath));
                }
                else
                {
                    AnalysisSummary = string.Format(
                        "{0} is valid for naming policy {1}. Structure preview includes {2} assembly file(s) and {3} component file(s).",
                        analysis.ProjectCode ?? "Folder",
                        NamingPolicyVersion,
                        analysis.AssemblyFiles.Count,
                        analysis.DetailFiles.Count);
                }

                StatusMessage = string.Format(
                    "{0} tracked, {1} ignored, primary assembly {2}. Push can continue.",
                    analysis.TrackedFiles.Count,
                    analysis.IgnoredFiles.Count,
                    analysis.PrimaryAssembly?.FileName ?? "-");
                return;
            }

            AnalysisSummary = string.Format(
                "Naming validation found {0} blocking issue(s). Review Naming Preview before push.",
                BlockingIssueCount);
            StatusMessage = AnalysisSummary;
        }

        private void BuildPushPreview(PdmFolderAnalysis folderAnalysis, PdmBusinessStructureAnalysis businessAnalysis)
        {
            var analyzeResult = PushPreviewMapper.ToAnalyzeResult(folderAnalysis, businessAnalysis);
            if (analyzeResult == null)
                return;

            var builder = new PdmPushPreviewBuilder();
            _pushPreview = builder.Build(analyzeResult, SelectedBranch, CommitMessage);

            PreviewParts.Clear();
            PreviewCads.Clear();
            PreviewDocuments.Clear();
            PreviewIgnoredFiles.Clear();

            foreach (var part in _pushPreview.Parts)
                PreviewParts.Add(part);

            foreach (var cad in _pushPreview.Cads)
                PreviewCads.Add(cad);

            foreach (var doc in _pushPreview.Documents)
                PreviewDocuments.Add(doc);

            foreach (var ignored in _pushPreview.IgnoredFiles)
                PreviewIgnoredFiles.Add(ignored);
        }

        private static PdmAnalysisSources ResolveAnalysisSources(string folderPath)
        {
            var result = new PdmAnalysisSources
            {
                SelectedFolder = folderPath
            };

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return result;
            }

            var directory = new DirectoryInfo(folderPath);
            var parentPath = directory.Parent?.FullName;

            if (directory.Name.Equals("ARAS01", StringComparison.OrdinalIgnoreCase))
            {
                result.CadFolder = folderPath;
                result.PackageFolder = parentPath == null ? null : Path.Combine(parentPath, "StudyCase_0603");
                if (!Directory.Exists(result.PackageFolder))
                {
                    result.PackageFolder = null;
                }
                return result;
            }

            if (directory.Name.Equals("StudyCase_0603", StringComparison.OrdinalIgnoreCase))
            {
                result.PackageFolder = folderPath;
                result.CadFolder = parentPath == null ? null : Path.Combine(parentPath, "ARAS01");
                if (!Directory.Exists(result.CadFolder))
                {
                    result.CadFolder = null;
                }
                return result;
            }

            if (LooksLikeBusinessPackageFolder(folderPath) && directory.Parent != null)
            {
                var siblingCadFolder = Path.Combine(directory.Parent.FullName, "ARAS01");
                if (Directory.Exists(siblingCadFolder))
                {
                    result.PackageFolder = folderPath;
                    result.CadFolder = siblingCadFolder;
                    return result;
                }
            }

            if (directory.Parent != null &&
                directory.Parent.Name.Equals("StudyCase_0603", StringComparison.OrdinalIgnoreCase) &&
                LooksLikeBusinessPackageFolder(folderPath))
            {
                result.PackageFolder = folderPath;
                result.CadFolder = directory.Parent.Parent == null
                    ? null
                    : Path.Combine(directory.Parent.Parent.FullName, "ARAS01");

                if (!Directory.Exists(result.CadFolder))
                {
                    result.CadFolder = null;
                }

                return result;
            }

            if (LooksLikeBusinessPackageFolder(folderPath))
            {
                result.PackageFolder = folderPath;
                result.CadFolder = null;
                return result;
            }

            result.CadFolder = folderPath;
            return result;
        }

        private static bool LooksLikeBusinessPackageFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return false;
            }

            var hasGroupPdf = Directory
                .GetFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Any(fileName =>
                    !string.IsNullOrWhiteSpace(fileName) &&
                    System.Text.RegularExpressions.Regex.IsMatch(
                        fileName,
                        @"^\d{2}[A-Z]?\. .+\.pdf$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase));

            var hasRootDwg = Directory
                .GetFiles(folderPath, "*.dwg", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Any(fileName =>
                    !string.IsNullOrWhiteSpace(fileName) &&
                    System.Text.RegularExpressions.Regex.IsMatch(
                        fileName,
                        @"^[A-Za-z0-9][A-Za-z0-9 -]*_Ver\d+\.\d+$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase));

            return hasGroupPdf && hasRootDwg;
        }

        private static PdmNamingPolicy LoadPolicy()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdm-naming-policy.json");
            if (!File.Exists(path))
            {
                return new PdmNamingPolicy();
            }

            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<PdmNamingPolicy>(json) ?? new PdmNamingPolicy();
        }

        private static string GetDefaultSampleFolder()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var studyCase = Path.Combine(userProfile, "Research", "ArasInnovator", "StudyCase_0603");
            if (Directory.Exists(studyCase))
            {
                return studyCase;
            }

            var aras01 = Path.Combine(userProfile, "Research", "ArasInnovator", "ARAS01");
            return Directory.Exists(aras01) ? aras01 : string.Empty;
        }

        public void RefreshConnectionStatus()
        {
            var isConnected = MainViewModel.SharedPdmClient != null;
            ConnectionDisplayName = isConnected ? "Connected to Aras" : "Preview mode";
            ConnectionDatabase = isConnected ? "Aras session active" : "Local analysis — not connected to Aras";
        }

        private static string FormatBusinessNodeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "-";
            }

            return rawName.Trim().Replace(" ", string.Empty).ToUpperInvariant();
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class PdmStructureNode
    {
        public PdmStructureNode(
            string name,
            string partCode,
            string nodeType,
            int quantity,
            string revision,
            string state,
            string accent,
            ObservableCollection<PdmStructureNode> children = null,
            string primaryCad = null,
            string lockedBy = null,
            string sourceDocument = null)
        {
            Name = name;
            PartCode = partCode;
            NodeType = nodeType;
            Quantity = quantity;
            Revision = revision;
            State = state;
            Accent = accent;
            Children = children ?? new ObservableCollection<PdmStructureNode>();
            PrimaryCad = primaryCad ?? "-";
            LockedBy = lockedBy ?? "-";
            SourceDocument = sourceDocument ?? "-";
        }

        public string Name { get; }
        public string PartCode { get; }
        public string NodeType { get; }
        public int Quantity { get; }
        public string Revision { get; }
        public string State { get; }
        public string Accent { get; }
        public string PrimaryCad { get; }
        public string LockedBy { get; }
        public string SourceDocument { get; }
        public ObservableCollection<PdmStructureNode> Children { get; }
    }

    public sealed class PdmFileChange
    {
        public PdmFileChange(string changeType, string fileName, string accent)
        {
            ChangeType = changeType;
            FileName = fileName;
            Accent = accent;
        }

        public string ChangeType { get; }
        public string FileName { get; }
        public string Accent { get; }
    }

    public sealed class PdmDocumentItem
    {
        public PdmDocumentItem(string name, string kind, string sourcePath = null)
        {
            Name = name;
            Kind = kind;
            SourcePath = sourcePath;
        }

        public string Name { get; }
        public string Kind { get; }
        public string SourcePath { get; }
        public bool IsPdf => SourcePath != null &&
            string.Equals(System.IO.Path.GetExtension(SourcePath), ".pdf", StringComparison.OrdinalIgnoreCase);
        public bool CanOpen => SourcePath != null && System.IO.File.Exists(SourcePath);
        public string OpenLabel => IsPdf ? "Open PDF" : "Open File";
    }

    public sealed class PdmNamingPreviewItem
    {
        public PdmNamingPreviewItem(
            string fileName,
            string nodeType,
            string logicalPartCode,
            string version,
            string revision,
            string sequence,
            string status)
        {
            FileName = fileName;
            NodeType = nodeType;
            LogicalPartCode = logicalPartCode;
            Version = version;
            Revision = revision;
            Sequence = sequence;
            Status = status;
        }

        public string FileName { get; }
        public string NodeType { get; }
        public string LogicalPartCode { get; }
        public string Version { get; }
        public string Revision { get; }
        public string Sequence { get; }
        public string Status { get; }
    }

    public sealed class PdmProjectFileNode
    {
        public PdmProjectFileNode(string name, string relativePath, bool isFolder, string kind)
        {
            Name = name;
            RelativePath = relativePath;
            IsFolder = isFolder;
            Kind = kind;
            Children = new ObservableCollection<PdmProjectFileNode>();
        }

        public string Name { get; }
        public string RelativePath { get; }
        public bool IsFolder { get; }
        public string Kind { get; }
        public ObservableCollection<PdmProjectFileNode> Children { get; }
    }

    public sealed class PdmAnalysisSources
    {
        public string SelectedFolder { get; set; }

        public string CadFolder { get; set; }

        public string PackageFolder { get; set; }
    }
}

