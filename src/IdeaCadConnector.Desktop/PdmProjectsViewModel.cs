using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
        private PdmFolderAnalysis _latestAnalysis;
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

            SelectedBranch = Branches[0];
            SelectedCommit = Commits[0];
            FolderPath = GetDefaultSampleFolder();
            AnalysisSummary = "Select a project folder to preview the product structure.";
            StatusMessage = "PDM repository preview is ready.";

            CloneCommand = new RelayCommand(_ => StatusMessage = "Clone will use the selected repository and commit.");
            PullCommand = new RelayCommand(_ => StatusMessage = "Pull is not connected to Aras yet.");
            _pushCommand = new RelayCommand(_ => PushWorkspace(), _ => CanPush);
            PushCommand = _pushCommand;
            NewBranchCommand = new RelayCommand(_ => StatusMessage = "New branch will start from the selected commit.");
            _refreshCommand = new RelayCommand(_ => AnalyzeFolder());
            RefreshCommand = _refreshCommand;
            _analyzeFolderCommand = new RelayCommand(_ => AnalyzeFolder());
            AnalyzeFolderCommand = _analyzeFolderCommand;
            _browseFolderCommand = new RelayCommand(_ => BrowseFolder());
            BrowseFolderCommand = _browseFolderCommand;

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

        public bool CanPush => _latestAnalysis != null && _latestAnalysis.IsValid && TrackedFileCount > 0;

        public PdmStructureNode SelectedNode
        {
            get => _selectedNode;
            set => SetField(ref _selectedNode, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public ICommand CloneCommand { get; }
        public ICommand PullCommand { get; }
        public ICommand PushCommand { get; }
        public ICommand NewBranchCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AnalyzeFolderCommand { get; }
        public ICommand BrowseFolderCommand { get; }

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

        private void PushWorkspace()
        {
            if (!CanPush)
            {
                StatusMessage = "Push is blocked until the folder passes naming validation.";
                return;
            }

            StatusMessage = string.Format(
                "Ready to push {0} tracked file(s) from {1} into repository {2}.",
                TrackedFileCount,
                Path.GetFileName(FolderPath.TrimEnd(Path.DirectorySeparatorChar)),
                SelectedRepository ?? "-");
        }

        private void AnalyzeFolder()
        {
            var policy = LoadPolicy();
            NamingPolicyVersion = policy.PolicyVersion;

            _latestAnalysis = new Aras01FolderAnalyzer(policy).Analyze(FolderPath);
            Structure.Clear();
            Changes.Clear();
            Documents.Clear();
            NamingPreview.Clear();
            Repositories.Clear();
            ProjectFiles.Clear();
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
            BuildStructure(_latestAnalysis);
            BuildDocuments(_latestAnalysis);
            BuildProjectFiles(_latestAnalysis);
            BuildSummary(_latestAnalysis);

            _pushCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
            _analyzeFolderCommand.RaiseCanExecuteChanged();
            _browseFolderCommand.RaiseCanExecuteChanged();
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

        private void BuildStructure(PdmFolderAnalysis analysis)
        {
            if (string.IsNullOrWhiteSpace(analysis.ProjectCode))
            {
                return;
            }

            var root = new PdmStructureNode(
                analysis.ProjectCode,
                analysis.ProjectCode,
                "Assembly",
                1,
                analysis.PrimaryAssembly?.Revision ?? "-",
                analysis.IsValid ? "Ready to push" : "Fix naming",
                "#FF7C47DC",
                primaryCad: analysis.PrimaryAssembly?.FileName ?? "-");

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
                    primaryCad: detail.FileName));
            }

            Structure.Add(root);
            SelectedNode = root;
        }

        private void BuildDocuments(PdmFolderAnalysis analysis)
        {
            foreach (var olderAssembly in analysis.AssemblyFiles
                .Where(file => !ReferenceEquals(file, analysis.PrimaryAssembly))
                .OrderBy(file => file.FileName))
            {
                Documents.Add(new PdmDocumentItem(
                    olderAssembly.FileName,
                    "DWG REV " + olderAssembly.Revision));
            }

            foreach (var ignored in analysis.IgnoredFiles.OrderBy(file => file.FileName))
            {
                Documents.Add(new PdmDocumentItem(
                    ignored.FileName,
                    "Ignored backup"));
            }
        }

        private void BuildProjectFiles(PdmFolderAnalysis analysis)
        {
            var root = new PdmProjectFileNode(
                string.IsNullOrWhiteSpace(analysis.FolderPath)
                    ? "No folder selected"
                    : new DirectoryInfo(analysis.FolderPath).Name,
                analysis.FolderPath,
                true,
                "Folder");

            var lookup = new System.Collections.Generic.Dictionary<string, PdmProjectFileNode>(StringComparer.OrdinalIgnoreCase)
            {
                { string.Empty, root }
            };

            foreach (var file in analysis.TrackedFiles.Concat(analysis.IgnoredFiles).OrderBy(item => item.RelativePath))
            {
                var segments = file.RelativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                var currentKey = string.Empty;
                var parent = root;

                for (var index = 0; index < segments.Length - 1; index++)
                {
                    currentKey = currentKey.Length == 0
                        ? segments[index]
                        : currentKey + Path.DirectorySeparatorChar + segments[index];

                    if (!lookup.TryGetValue(currentKey, out var folderNode))
                    {
                        folderNode = new PdmProjectFileNode(segments[index], currentKey, true, "Folder");
                        parent.Children.Add(folderNode);
                        lookup[currentKey] = folderNode;
                    }

                    parent = folderNode;
                }

                parent.Children.Add(new PdmProjectFileNode(
                    segments.Last(),
                    file.RelativePath,
                    false,
                    file.IsIgnored ? "Ignored" : file.NodeType));
            }

            ProjectFiles.Add(root);
        }

        private void BuildSummary(PdmFolderAnalysis analysis)
        {
            if (!Directory.Exists(FolderPath))
            {
                AnalysisSummary = "Select an existing project folder before analyzing the naming policy.";
                StatusMessage = "Folder path is missing or invalid.";
                return;
            }

            if (analysis.IsValid)
            {
                AnalysisSummary = string.Format(
                    "{0} is valid for naming policy {1}. Structure preview includes {2} assembly file(s) and {3} component file(s).",
                    analysis.ProjectCode ?? "Folder",
                    NamingPolicyVersion,
                    analysis.AssemblyFiles.Count,
                    analysis.DetailFiles.Count);

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
            var path = Path.Combine(userProfile, "Research", "ArasInnovator", "ARAS01");
            return Directory.Exists(path) ? path : string.Empty;
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
            string lockedBy = null)
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
        public PdmDocumentItem(string name, string kind)
        {
            Name = name;
            Kind = kind;
        }

        public string Name { get; }
        public string Kind { get; }
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
}
