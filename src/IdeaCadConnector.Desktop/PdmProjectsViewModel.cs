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
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
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
        private readonly RelayCommand _startDetailedDesignCommand;
        private readonly RelayCommand _submitForReviewCommand;
        private readonly RelayCommand _approveCadCommand;
        private readonly RelayCommand _requestReworkCommand;
        private readonly Dictionary<string, List<PdmDocumentItem>> _documentsByPartCode = new Dictionary<string, List<PdmDocumentItem>>(StringComparer.OrdinalIgnoreCase);
        private PdmFolderAnalysis _latestAnalysis;
        private PdmBusinessStructureAnalysis _latestBusinessStructure;
        private PdmAnalysisSources _latestSources;
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
        private int _previewRefreshVersion;
        private string _connectionDisplayName;
        private string _connectionDatabase;
        private bool _isAnalyzing;
        private bool _isPushing;
        private bool _isOpeningInIronCad;
        private bool _isCadSectionExpanded = true;
        private bool _isDocumentsSectionExpanded = true;
        private PushPreview _pushPreview;
        private string _commitMessage;

        private CheckoutService _checkoutService;
        private WorkspaceService _workspaceService;
        private string _cadLockStateText;
        private string _lockedByText;
        private string _cadFileStateText;
        private string _cadRevisionText;
        private string _cadGenerationText;
        private string _cadLifecycleText;
        private string _cadEditPolicyText;
        private string _liveCadId;
        private string _liveCadState;
        private string _liveCadRevision;
        private int _liveCadGeneration;
        private bool _liveHasNativeFile;
        private bool _isCheckedOutByMe;
        private bool _isCheckedOutByOther;
        private bool _isAvailable;
        private CadOperationContext _cadOperationContext;

        public PdmProjectsViewModel()
        {
            _workspaceService = new WorkspaceService(new WorkspaceOptions());
            Repositories = new ObservableCollection<string>();
            Branches = new ObservableCollection<string> { "main", "experiment" };
            Commits = new ObservableCollection<string>
            {
                "Working folder analysis",
                "C1 - Initial project import"
            };
            PdmStructure = new ObservableCollection<PdmStructureNode>();
            CadStructure = new ObservableCollection<PdmStructureNode>();
            StructureMappings = new ObservableCollection<PdmStructureMappingItem>();
            Changes = new ObservableCollection<PdmFileChange>();
            Documents = new ObservableCollection<PdmDocumentItem>();
            NamingPreview = new ObservableCollection<PdmNamingPreviewItem>();
            ProjectFiles = new ObservableCollection<PdmProjectFileNode>();
            PreviewParts = new ObservableCollection<PartPreviewRow>();
            PreviewCads = new ObservableCollection<CadPreviewRow>();
            PreviewDocuments = new ObservableCollection<DocumentPreviewRow>();
            PreviewIgnoredFiles = new ObservableCollection<IgnoredPreviewRow>();
            RelatedFiles = new ObservableCollection<PdmDocumentItem>();

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
            OpenInIronCadCommand = new RelayCommand(_ => OpenInIronCadAsync(), _ => CanOpenInIronCad);
            CheckInCommand = new RelayCommand(_ => CheckInAsync(), _ => IsCheckedOutByMe);
            CancelCheckoutCommand = new RelayCommand(_ => CancelCheckoutAsync(), _ => IsCheckedOutByMe);
            _startDetailedDesignCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.StartDetailedDesign), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.StartDetailedDesign));
            StartDetailedDesignCommand = _startDetailedDesignCommand;
            _submitForReviewCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.SubmitForReview), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.SubmitForReview));
            SubmitForReviewCommand = _submitForReviewCommand;
            _approveCadCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.Approve), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            ApproveCadCommand = _approveCadCommand;
            _requestReworkCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.RequestRework), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.RequestRework));
            RequestReworkCommand = _requestReworkCommand;
            ToggleCadSectionCommand = new RelayCommand(_ => ToggleCadSection());
            ToggleDocumentsSectionCommand = new RelayCommand(_ => ToggleDocumentsSection());


            AnalyzeFolder();
        }

        public ObservableCollection<string> Repositories { get; }
        public ObservableCollection<string> Branches { get; }
        public ObservableCollection<string> Commits { get; }
        public ObservableCollection<PdmStructureNode> PdmStructure { get; }
        public ObservableCollection<PdmStructureNode> CadStructure { get; }
        public ObservableCollection<PdmStructureMappingItem> StructureMappings { get; }
        public ObservableCollection<PdmFileChange> Changes { get; }
        public ObservableCollection<PdmDocumentItem> Documents { get; }
        public ObservableCollection<PdmNamingPreviewItem> NamingPreview { get; }
        public ObservableCollection<PdmProjectFileNode> ProjectFiles { get; }
        public ObservableCollection<PartPreviewRow> PreviewParts { get; }
        public ObservableCollection<CadPreviewRow> PreviewCads { get; }
        public ObservableCollection<DocumentPreviewRow> PreviewDocuments { get; }
        public ObservableCollection<IgnoredPreviewRow> PreviewIgnoredFiles { get; }
        public ObservableCollection<PdmDocumentItem> RelatedFiles { get; }

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
                    RefreshPushPreview();
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
            MainViewModel.SharedPdmClient != null &&
            IsMainBranch;

        public bool HasPdmStructure => PdmStructure.Count > 0;
        public bool HasCadStructure => CadStructure.Count > 0;
        public bool HasStructure => HasPdmStructure || HasCadStructure;
        public bool HasStructureMappings => StructureMappings.Count > 0;
        public bool HasProjectFiles => ProjectFiles.Count > 0;
        public bool HasChanges => Changes.Count > 0;
        public bool HasNamingPreview => NamingPreview.Count > 0;
        public bool HasDocuments => Documents.Count > 0;
        public bool HasRelatedFiles => RelatedFiles.Count > 0;
        public bool HasSelectedNode => SelectedNode != null;

        public PdmStructureNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetField(ref _selectedNode, value))
                {
                    OnPropertyChanged(nameof(HasSelectedNode));
                    OnPropertyChanged(nameof(CanOpenInIronCad));
                    ((RelayCommand)OpenInIronCadCommand).RaiseCanExecuteChanged();
                    RefreshSelectedDocuments();
                    _ = RefreshCadStateAsync();
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

        public bool IsOpeningInIronCad
        {
            get => _isOpeningInIronCad;
            set
            {
                if (SetField(ref _isOpeningInIronCad, value))
                {
                    OnPropertyChanged(nameof(CanOpenInIronCad));
                    ((RelayCommand)OpenInIronCadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsCadSectionExpanded
        {
            get => _isCadSectionExpanded;
            set
            {
                if (SetField(ref _isCadSectionExpanded, value))
                {
                    OnPropertyChanged(nameof(IsCadSectionExpanded));
                    OnPropertyChanged(nameof(CadSectionIcon));
                }
            }
        }

        public bool IsDocumentsSectionExpanded
        {
            get => _isDocumentsSectionExpanded;
            set
            {
                if (SetField(ref _isDocumentsSectionExpanded, value))
                {
                    OnPropertyChanged(nameof(IsDocumentsSectionExpanded));
                    OnPropertyChanged(nameof(DocumentsSectionIcon));
                }
            }
        }

        public string CadSectionIcon => IsCadSectionExpanded ? "\u25C0" : "\u25B6";
        public string DocumentsSectionIcon => IsDocumentsSectionExpanded ? "\u25C0" : "\u25B6";

        public void ToggleCadSection() => IsCadSectionExpanded = !IsCadSectionExpanded;
        public void ToggleDocumentsSection() => IsDocumentsSectionExpanded = !IsDocumentsSectionExpanded;

        public bool CanOpenInIronCad =>
            !IsOpeningInIronCad &&
            SelectedNode != null &&
            !IsSelectedRootAssemblyNode() &&
            !string.IsNullOrWhiteSpace(SelectedNode.PrimaryCad) &&
            SelectedNode.PrimaryCad != "-" &&
            MainViewModel.SharedArasCadClient != null &&
            !string.IsNullOrWhiteSpace(_liveCadId);

        public string CommitMessage
        {
            get => _commitMessage;
            set
            {
                if (SetField(ref _commitMessage, value))
                {
                    RefreshPushPreview();
                }
            }
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
        public ICommand OpenInIronCadCommand { get; }
        public ICommand CheckInCommand { get; }
        public ICommand CancelCheckoutCommand { get; }
        public ICommand StartDetailedDesignCommand { get; }
        public ICommand SubmitForReviewCommand { get; }
        public ICommand ApproveCadCommand { get; }
        public ICommand RequestReworkCommand { get; }
        public ICommand ToggleCadSectionCommand { get; }
        public ICommand ToggleDocumentsSectionCommand { get; }

        public string CadLockStateText
        {
            get => _cadLockStateText ?? "Unknown";
            set => SetField(ref _cadLockStateText, value);
        }

        public string LockedByText
        {
            get => _lockedByText ?? "-";
            set => SetField(ref _lockedByText, value);
        }

        public string CadFileStateText
        {
            get => _cadFileStateText ?? "Unknown";
            set => SetField(ref _cadFileStateText, value);
        }

        public string CadRevisionText
        {
            get => _cadRevisionText ?? "-";
            set => SetField(ref _cadRevisionText, value);
        }

        public string CadGenerationText
        {
            get => _cadGenerationText ?? "-";
            set => SetField(ref _cadGenerationText, value);
        }

        public string CadLifecycleText
        {
            get => _cadLifecycleText ?? "-";
            set => SetField(ref _cadLifecycleText, value);
        }

        public string CadEditPolicyText
        {
            get => _cadEditPolicyText ?? "-";
            set => SetField(ref _cadEditPolicyText, value);
        }

        public bool IsCheckedOutByMe
        {
            get => _isCheckedOutByMe;
            set
            {
                if (SetField(ref _isCheckedOutByMe, value))
                {
                    ((RelayCommand)CheckInCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)CancelCheckoutCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsCheckedOutByOther
        {
            get => _isCheckedOutByOther;
            set => SetField(ref _isCheckedOutByOther, value);
        }

        public bool IsAvailable
        {
            get => _isAvailable;
            set => SetField(ref _isAvailable, value);
        }

        public string WorkflowStatusText
        {
            get
            {
                if (_cadOperationContext?.ActiveTask == null)
                {
                    if (HasCadAction(CadBusinessActionKind.StartDetailedDesign))
                        return "Initial CAD is ready to move into detailed design.";
                    if (HasCadAction(CadBusinessActionKind.SubmitForReview))
                        return "Design is ready to submit for review.";
                    return "No active workflow task.";
                }

                var paths = _cadOperationContext.ActiveTask.AvailablePaths;
                var openPaths = paths?.Count(p => !p.IsComplete) ?? 0;
                return $"Task: {_cadOperationContext.ActiveTask.ActivityName} ({openPaths} action(s) available)";
            }
        }

        public bool HasAnyCadBusinessAction =>
            HasCadAction(CadBusinessActionKind.StartDetailedDesign) ||
            HasCadAction(CadBusinessActionKind.SubmitForReview) ||
            HasCadAction(CadBusinessActionKind.Approve) ||
            HasCadAction(CadBusinessActionKind.RequestRework);

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
                    var msg = result.StagingOnly
                        ? $"Staging snapshot created for branch '{request.TargetBranch}'. Live business data was not updated. Commit: {result.CommitId ?? "-"}"
                        : string.Format(
                            "Push complete. Created/Reused {0} part(s), {1} CAD(s), {2} document(s). Commit: {3}",
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
                    SourceFilePath = c.SourceFilePath,
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
                        row.Action = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
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
                    {
                        row.Action = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
                    }
                    else if (!string.IsNullOrWhiteSpace(res.ArasId))
                    {
                        var metaAction = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
                        row.Action = metaAction + " (file failed): " + (res.ErrorMessage ?? "Unknown");
                    }
                    else
                    {
                        row.Action = "Failed: " + (res.ErrorMessage ?? "Unknown");
                    }
                }
            }

            if (result.DocumentResults != null)
            {
                for (int i = 0; i < result.DocumentResults.Count && i < PreviewDocuments.Count; i++)
                {
                    var res = result.DocumentResults[i];
                    var row = PreviewDocuments[i];
                    if (res.Success)
                        row.Action = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
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
            _latestSources = sources;
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

            PdmStructure.Clear();
            CadStructure.Clear();
            StructureMappings.Clear();
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
            BuildPdmStructure(_latestAnalysis, _latestBusinessStructure);
            BuildCadStructure(_latestAnalysis, _latestBusinessStructure);
            BuildStructureMappings(_latestAnalysis, _latestBusinessStructure);
            BuildDocuments(_latestAnalysis, _latestBusinessStructure, sources.PackageFolder ?? FolderPath, sources.CadFolder ?? FolderPath);
            BuildProjectFiles(sources);
            BuildSummary(_latestAnalysis, _latestBusinessStructure, sources);
            BuildPushPreview(_latestAnalysis, _latestBusinessStructure);
            _ = RefreshPreviewFromServerAsync();

            OnPropertyChanged(nameof(HasPdmStructure));
            OnPropertyChanged(nameof(HasCadStructure));
            OnPropertyChanged(nameof(HasStructureMappings));
            OnPropertyChanged(nameof(HasStructure));
            OnPropertyChanged(nameof(HasProjectFiles));
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(HasNamingPreview));
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(HasRelatedFiles));
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

        private void BuildPdmStructure(PdmFolderAnalysis analysis, PdmBusinessStructureAnalysis businessStructure)
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
                businessStructure != null && businessStructure.HasStructure ? "Business structure preview" : (analysis.IsValid ? "Ready to push" : "Fix naming"),
                "#FF7C47DC",
                perspective: "PDM",
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

            PdmStructure.Add(root);
            SelectedNode = root;
        }

        private void BuildCadStructure(PdmFolderAnalysis analysis, PdmBusinessStructureAnalysis businessStructure)
        {
            if (analysis == null)
            {
                return;
            }

            var projectCode = string.IsNullOrWhiteSpace(analysis.ProjectCode) ? "CAD" : analysis.ProjectCode;
            var rootName = analysis.PrimaryAssembly?.FileName
                ?? businessStructure?.RootDrawingFileName
                ?? projectCode;

            var root = new PdmStructureNode(
                rootName,
                projectCode,
                "Assembly",
                1,
                analysis.PrimaryAssembly?.Revision ?? "-",
                "Parsed from CAD folder",
                "#FF2967EF",
                perspective: "CAD",
                primaryCad: analysis.PrimaryAssembly?.FileName ?? "-",
                sourceDocument: analysis.PrimaryAssembly?.FileName ?? "-");

            foreach (var detail in analysis.DetailFiles.OrderBy(file => file.Sequence ?? int.MaxValue).ThenBy(file => file.FileName))
            {
                root.Children.Add(new PdmStructureNode(
                    detail.DisplayName,
                    detail.LogicalPartCode,
                    "Component",
                    1,
                    detail.Version ?? "-",
                    "Parsed from CAD name",
                    "#FF1F9D55",
                    perspective: "CAD",
                    primaryCad: detail.FileName,
                    sourceDocument: detail.FileName));
            }

            if (analysis.PrimaryAssembly != null || root.Children.Count > 0)
            {
                CadStructure.Add(root);
            }
        }

        private PdmStructureNode CreateBusinessStructureNode(
            string projectCode,
            PdmBusinessNode businessNode,
            string parentCode,
            IDictionary<string, PdmParsedFile> detailCadMap)
        {
            var normalizedName = FormatBusinessNodeName(businessNode.Name);
            var logicalCode = string.IsNullOrWhiteSpace(businessNode.Code)
                ? (string.IsNullOrWhiteSpace(parentCode) ? normalizedName : parentCode + "__" + normalizedName)
                : businessNode.Code;
            var primaryCad = ResolvePrimaryCadForNode(businessNode, logicalCode, detailCadMap);

            var node = new PdmStructureNode(
                normalizedName,
                logicalCode,
                businessNode.NodeType,
                1,
                "-",
                "Package inferred",
                businessNode.NodeType == "Assembly" ? "#FF2967EF" : "#FF1F9D55",
                perspective: "PDM",
                primaryCad: primaryCad,
                sourceDocument: businessNode.SourceFileName);

            foreach (var child in businessNode.Children)
            {
                node.Children.Add(CreateBusinessStructureNode(projectCode, child, logicalCode, detailCadMap));
            }

            return node;
        }

        private void BuildStructureMappings(PdmFolderAnalysis analysis, PdmBusinessStructureAnalysis businessStructure)
        {
            if (analysis == null)
            {
                return;
            }

            if (analysis.PrimaryAssembly != null)
            {
                StructureMappings.Add(new PdmStructureMappingItem(
                    analysis.ProjectCode ?? "PROJECT",
                    analysis.ProjectCode ?? "Project Root",
                    "Assembly",
                    analysis.PrimaryAssembly.FileName,
                    "Root CAD mapped"));
            }

            if (businessStructure == null || !businessStructure.HasStructure)
            {
                foreach (var detail in analysis.DetailFiles.OrderBy(file => file.Sequence ?? int.MaxValue))
                {
                    StructureMappings.Add(new PdmStructureMappingItem(
                        detail.LogicalPartCode ?? detail.FileName,
                        detail.DisplayName ?? detail.FileName,
                        "Component",
                        detail.FileName,
                        "Direct CAD node"));
                }

                return;
            }

            var detailCadMap = BuildDetailCadMap(analysis, businessStructure);
            foreach (var businessNode in FlattenBusinessNodes(businessStructure.RootNodes))
            {
                string mappedCad = "-";
                string status;

                if (businessNode.NodeType == "Assembly")
                {
                    status = "Business grouping";
                }
                else if (!string.IsNullOrWhiteSpace(businessNode.SourceFileName) &&
                         detailCadMap.TryGetValue(businessNode.SourceFileName, out var detailCad))
                {
                    mappedCad = detailCad.FileName;
                    status = "Mapped to CAD";
                }
                else
                {
                    status = "Missing CAD mapping";
                }

                StructureMappings.Add(new PdmStructureMappingItem(
                    businessNode.Code ?? "-",
                    FormatBusinessNodeName(businessNode.Name),
                    businessNode.NodeType ?? "-",
                    mappedCad,
                    status));
            }
        }

        private static IEnumerable<PdmBusinessNode> FlattenBusinessNodes(IEnumerable<PdmBusinessNode> nodes)
        {
            if (nodes == null)
            {
                yield break;
            }

            foreach (var node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                yield return node;
                foreach (var child in FlattenBusinessNodes(node.Children))
                {
                    yield return child;
                }
            }
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
                var logicalCode = string.IsNullOrWhiteSpace(node.Code)
                    ? (string.IsNullOrWhiteSpace(parentCode) ? normalizedName : parentCode + "__" + normalizedName)
                    : node.Code;

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
            RelatedFiles.Clear();

            if (SelectedNode == null || string.IsNullOrWhiteSpace(SelectedNode.PartCode))
            {
                return;
            }

            if (_documentsByPartCode.TryGetValue(SelectedNode.PartCode, out var selectedDocuments))
            {
                foreach (var document in selectedDocuments)
                {
                    Documents.Add(document);
                    RelatedFiles.Add(document);
                }
            }

            var primaryCad = SelectedNode.PrimaryCad;
            if (!string.IsNullOrWhiteSpace(primaryCad) && primaryCad != "-")
            {
                var alreadyPresent = RelatedFiles.Any(f =>
                    string.Equals(f.Name, primaryCad, StringComparison.OrdinalIgnoreCase));
                if (!alreadyPresent)
                {
                    var sourcePath = ResolvePrimaryCadPath(primaryCad);
                    var fileKind = string.Equals(SelectedNode.NodeType, "Assembly", StringComparison.OrdinalIgnoreCase)
                        ? "CAD assembly"
                        : "CAD component";
                    RelatedFiles.Add(new PdmDocumentItem(primaryCad, fileKind, sourcePath));
                }
            }

            OnPropertyChanged(nameof(HasRelatedFiles));
        }

        private string ResolvePrimaryCadPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || _latestSources == null)
                return null;

            var candidates = new[]
            {
                _latestSources.CadFolder,
                _latestSources.PackageFolder,
                _latestSources.SelectedFolder
            };

            foreach (var folder in candidates)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;
                var path = System.IO.Path.Combine(folder, fileName);
                if (System.IO.File.Exists(path))
                    return path;
            }

            return null;
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

        private async void OpenInIronCadAsync()
        {
            if (!CanOpenInIronCad)
                return;

            var cadClient = MainViewModel.SharedArasCadClient;
            if (cadClient == null)
            {
                StatusMessage = "Not connected to Aras. Sign in first.";
                return;
            }

            IsOpeningInIronCad = true;
            try
            {
                if (_checkoutService == null)
                {
                    _checkoutService = new CheckoutService(cadClient, _workspaceService);
                }

                var localDir = GetWorkspaceDirectory();
                var cadFileName = SelectedNode.PrimaryCad;
                var cadFolder = ResolvePrimaryCadFolder();
                var cadPath = string.IsNullOrWhiteSpace(cadFolder)
                    ? null
                    : System.IO.Path.Combine(cadFolder, cadFileName);

                if (string.IsNullOrWhiteSpace(_liveCadId))
                {
                    StatusMessage = "No CAD item found on Aras for this node. Push the project first.";
                    return;
                }

                var manifest = _workspaceService.LoadManifest(FolderPath);
                var validManifest = manifest != null &&
                    string.Equals(manifest.CadId, _liveCadId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(manifest.ProjectFolder, FolderPath, StringComparison.OrdinalIgnoreCase);

                if (validManifest &&
                    !string.IsNullOrWhiteSpace(manifest.LocalFilePath) &&
                    System.IO.File.Exists(manifest.LocalFilePath) &&
                    !string.IsNullOrWhiteSpace(manifest.LockToken))
                {
                    var adapter = new IronCadExternalAdapter();
                    await adapter.OpenDocumentAsync(manifest.LocalFilePath, CadOpenMode.Edit, CancellationToken.None);
                    StatusMessage = $"Opened {Path.GetFileName(manifest.LocalFilePath)} (checked out).";
                    return;
                }

                if (IsCheckedOutByOther)
                {
                    var roResult = await _checkoutService.OpenReadOnlyAsync(
                        _liveCadId, localDir, CancellationToken.None);
                    if (roResult.Success && roResult.LocalFilePath != null)
                    {
                        var adapter = new IronCadExternalAdapter();
                        await adapter.OpenDocumentAsync(roResult.LocalFilePath, CadOpenMode.ReadOnly, CancellationToken.None);
                        StatusMessage = $"Opened {cadFileName} in read-only mode.";
                    }
                    else
                    {
                        var fallbackPath = cadPath;
                        if (fallbackPath != null && System.IO.File.Exists(fallbackPath))
                        {
                            var adapter = new IronCadExternalAdapter();
                            await adapter.OpenDocumentAsync(fallbackPath, CadOpenMode.ReadOnly, CancellationToken.None);
                            StatusMessage = $"Opened {cadFileName} in read-only mode (local).";
                        }
                        else
                        {
                            StatusMessage = roResult.ErrorMessage ?? "Cannot open this CAD.";
                        }
                    }
                    return;
                }

                if (_liveHasNativeFile && !string.IsNullOrWhiteSpace(_liveCadState) &&
                    !CadLifecyclePolicy.CanCheckout(_liveCadState))
                {
                    var roResult = await _checkoutService.OpenReadOnlyAsync(
                        _liveCadId, localDir, CancellationToken.None);
                    if (roResult.Success && roResult.LocalFilePath != null)
                    {
                        var adapter = new IronCadExternalAdapter();
                        await adapter.OpenDocumentAsync(roResult.LocalFilePath, CadOpenMode.ReadOnly, CancellationToken.None);
                        StatusMessage = $"Opened {cadFileName} in read-only mode (lifecycle: {_liveCadState}).";
                    }
                    else
                    {
                        StatusMessage = CadLifecyclePolicy.GetCheckoutBlockedMessage(_liveCadState);
                    }
                    return;
                }

                var checkoutInfo = await _checkoutService.CheckoutAndDownloadAsync(
                    _liveCadId, localDir, CancellationToken.None);

                if (!checkoutInfo.Success)
                {
                    var isLockedError = checkoutInfo.ErrorMessage != null &&
                        checkoutInfo.ErrorMessage.Contains("locked", StringComparison.OrdinalIgnoreCase);
                    if (isLockedError)
                    {
                        if (cadPath != null && System.IO.File.Exists(cadPath))
                        {
                            var adapter = new IronCadExternalAdapter();
                            await adapter.OpenDocumentAsync(cadPath, CadOpenMode.ReadOnly, CancellationToken.None);
                            StatusMessage = $"Opened {cadFileName} in read-only mode (locked by another user).";
                        }
                        else
                        {
                            StatusMessage = "CAD is locked by another user and no local copy is available.";
                        }
                    }
                    else
                    {
                        StatusMessage = "Checkout failed: " + checkoutInfo.ErrorMessage;
                    }
                    return;
                }

                _workspaceService.SaveManifest(new WorkspaceManifest
                {
                    ProjectFolder = FolderPath,
                    PartNumber = SelectedNode.PartCode ?? "part",
                    CadId = _liveCadId,
                    CadNumber = checkoutInfo.Cad?.CadNumber,
                    NativeFileId = checkoutInfo.Cad?.NativeFileId,
                    LocalFilePath = checkoutInfo.LocalFilePath,
                    LockToken = checkoutInfo.LockToken,
                    LockedBy = MainViewModel.SharedUserName ?? "unknown",
                    CheckedOutAt = DateTime.UtcNow,
                    Branch = SelectedBranch
                });

                var editAdapter = new IronCadExternalAdapter();
                await editAdapter.OpenDocumentAsync(checkoutInfo.LocalFilePath, CadOpenMode.Edit, CancellationToken.None);
                StatusMessage = $"Checked out and opened {Path.GetFileName(checkoutInfo.LocalFilePath)}.";
                _ = RefreshCadStateAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to open in IronCAD: " + ex.Message;
            }
            finally
            {
                IsOpeningInIronCad = false;
            }
        }

        private async Task<string> ResolveCadIdForNodeAsync(PdmStructureNode node, CancellationToken ct)
        {
            var pdmClient = MainViewModel.SharedPdmClient;
            if (pdmClient == null) return null;

            var preview = _pushPreview;
            if (preview == null) return null;

            CadPreviewRow cad = null;

            if (!string.IsNullOrWhiteSpace(node.PartCode))
            {
                cad = preview.Cads.FirstOrDefault(c =>
                    string.Equals(c.LinkedPartLogicalCode, node.PartCode, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.LogicalCode, node.PartCode, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(node.PrimaryCad) && node.PrimaryCad != "-")
            {
                cad = cad ?? preview.Cads.FirstOrDefault(c =>
                    string.Equals(c.SourceFileName, node.PrimaryCad, StringComparison.OrdinalIgnoreCase));
            }

            if (cad == null || string.IsNullOrWhiteSpace(cad.CadNumber))
                return null;

            return await pdmClient.FindItemIdByNumberAsync("CAD", cad.CadNumber, ct);
        }

        private string GetWorkspaceDirectory()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = System.IO.Path.Combine(localAppData, "Idea", "ArasCadWorkspace", "PDM");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private string ResolvePrimaryCadFolder()
        {
            if (_latestSources != null)
            {
                if (!string.IsNullOrWhiteSpace(_latestSources.CadFolder))
                    return _latestSources.CadFolder;
                if (!string.IsNullOrWhiteSpace(_latestSources.PackageFolder))
                    return _latestSources.PackageFolder;
            }
            return FolderPath;
        }

        private async Task RefreshCadStateAsync()
        {
            var cadClient = MainViewModel.SharedArasCadClient;
            _liveCadId = null;
            _liveCadState = null;
            _liveCadRevision = null;
            _liveCadGeneration = 0;
            _liveHasNativeFile = false;
            _isCheckedOutByOther = false;
            SetCadOperationContext(null);

            if (SelectedNode == null || cadClient == null)
            {
                UpdateCadUiState();
                return;
            }

            var cadId = await ResolveCadIdForNodeAsync(SelectedNode, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(cadId))
            {
                UpdateCadUiState();
                return;
            }

            _liveCadId = cadId;

            try
            {
                var context = await cadClient.GetCadOperationContextAsync(cadId, CancellationToken.None);
                if (context != null)
                {
                    SetCadOperationContext(context);
                    _liveCadState = context.CadState;
                    _liveCadRevision = context.Revision;
                    _liveCadGeneration = context.Generation;
                    _liveHasNativeFile = context.HasNativeFile;
                    var currentUser = MainViewModel.SharedUserName ?? string.Empty;

                    if (context.IsLocked &&
                        !string.IsNullOrWhiteSpace(context.LockOwnerName))
                    {
                        _lockedByText = context.LockOwnerName;
                        _isCheckedOutByOther =
                            !string.Equals(context.LockOwnerName, currentUser, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        _lockedByText = null;
                        _isCheckedOutByOther = false;
                    }
                }
            }
            catch
            {
            }

            UpdateCadUiState();
        }

        private bool IsSelectedRootAssemblyNode()
        {
            return SelectedNode != null
                && PdmStructure.Count > 0
                && string.Equals(SelectedNode.PartCode, PdmStructure[0].PartCode, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateCadUiState()
        {
            if (IsSelectedRootAssemblyNode())
            {
                CadLockStateText = "Root assembly";
                LockedByText = "-";
                CadFileStateText = "Managed by assembly mapping/push flow";
                CadRevisionText = SelectedNode?.Revision ?? "-";
                CadGenerationText = "-";
                CadLifecycleText = "-";
                CadEditPolicyText = CadNodeHelper.GetRootAssemblyCadHint();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                return;
            }

            var currentUser = MainViewModel.SharedUserName ?? string.Empty;
            var manifest = _workspaceService.LoadManifest(FolderPath);
            var validManifest = manifest != null &&
                !string.IsNullOrWhiteSpace(_liveCadId) &&
                string.Equals(manifest.CadId, _liveCadId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(manifest.ProjectFolder, FolderPath, StringComparison.OrdinalIgnoreCase);

            var myLockInManifest = validManifest &&
                !string.IsNullOrWhiteSpace(manifest.LockToken) &&
                string.Equals(manifest.LockedBy, currentUser, StringComparison.OrdinalIgnoreCase);
            var myLocalFileExists = myLockInManifest &&
                !string.IsNullOrWhiteSpace(manifest.LocalFilePath) &&
                System.IO.File.Exists(manifest.LocalFilePath);

            if (string.IsNullOrWhiteSpace(_liveCadId))
            {
                CadLockStateText = "Not pushed";
                LockedByText = "-";
                CadFileStateText = "No CAD on Aras";
                CadRevisionText = SelectedNode?.Revision ?? "-";
                CadGenerationText = "-";
                CadLifecycleText = "-";
                CadEditPolicyText = "Push this node to Aras before revision-aware editing is available.";
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                return;
            }

            if (myLockInManifest && myLocalFileExists)
            {
                CadLockStateText = "Checked out by me";
                LockedByText = manifest.LockedBy;
                CadFileStateText = "Local working copy";
                ApplyCadRevisionState();
                IsCheckedOutByMe = true;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                return;
            }

            if (validManifest && !string.IsNullOrWhiteSpace(manifest.LockToken) &&
                !string.Equals(manifest.LockedBy, currentUser, StringComparison.OrdinalIgnoreCase))
            {
                CadLockStateText = "Local session stale";
                LockedByText = manifest.LockedBy ?? "-";
                CadFileStateText = "Session belongs to different user";
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                return;
            }

            if (IsCheckedOutByOther)
            {
                CadLockStateText = "Checked out by other";
                LockedByText = _lockedByText ?? "-";
                CadFileStateText = "Locked by another user";
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = true;
                IsAvailable = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_liveCadState) &&
                !CadLifecyclePolicy.CanCheckout(_liveCadState))
            {
                CadLockStateText = "Read-only";
                LockedByText = "-";
                CadFileStateText = CadLifecyclePolicy.GetCheckoutBlockedMessage(_liveCadState);
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                return;
            }

            if (!_liveHasNativeFile)
            {
                CadLockStateText = "Missing native file";
                LockedByText = "-";
                CadFileStateText = "No native file on Aras yet";
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = true;
                return;
            }

            CadLockStateText = "Available";
            LockedByText = "-";
            CadFileStateText = "Ready for checkout";
            ApplyCadRevisionState();
            IsCheckedOutByMe = false;
            IsCheckedOutByOther = false;
            IsAvailable = true;
        }

        private void ApplyCadRevisionState()
        {
            CadRevisionText = string.IsNullOrWhiteSpace(_liveCadRevision)
                ? (SelectedNode?.Revision ?? "-")
                : _liveCadRevision;
            CadGenerationText = _liveCadGeneration > 0
                ? _liveCadGeneration.ToString(CultureInfo.InvariantCulture)
                : "-";
            CadLifecycleText = string.IsNullOrWhiteSpace(_liveCadState) ? "-" : _liveCadState;
            CadEditPolicyText = BuildCadEditPolicyText(_liveCadState);
        }

        private static string BuildCadEditPolicyText(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return "Refresh Aras state before deciding whether this CAD can be edited or revised.";
            }

            if (CadLifecyclePolicy.CanCheckout(state))
            {
                return "Editable working state. Normal checkout/check-in is allowed.";
            }

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released))
            {
                return "Released CAD is read-only. The next step should be a new approved revision path, not direct editing.";
            }

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InReview))
            {
                return "CAD is under review. Finish review before more design edits or start the approved rework path.";
            }

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InChange))
            {
                return "CAD is already in a controlled change flow. Resume that approved path before editing again.";
            }

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Superseded) ||
                CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Obsolete))
            {
                return "This CAD is no longer active. Continue work through a replacement or new approved revision path.";
            }

            return CadLifecyclePolicy.GetCheckoutBlockedMessage(state);
        }

        private void SetCadOperationContext(CadOperationContext context)
        {
            _cadOperationContext = context;
            OnPropertyChanged(nameof(WorkflowStatusText));
            OnPropertyChanged(nameof(HasAnyCadBusinessAction));
            _startDetailedDesignCommand?.RaiseCanExecuteChanged();
            _submitForReviewCommand?.RaiseCanExecuteChanged();
            _approveCadCommand?.RaiseCanExecuteChanged();
            _requestReworkCommand?.RaiseCanExecuteChanged();
        }

        private bool HasCadAction(CadBusinessActionKind kind)
        {
            if (kind == CadBusinessActionKind.StartDetailedDesign)
                return !string.IsNullOrWhiteSpace(_liveCadState) && CadLifecyclePolicy.CanStartDetailedDesign(_liveCadState);

            if (kind == CadBusinessActionKind.SubmitForReview)
                return !string.IsNullOrWhiteSpace(_liveCadState) && CadLifecyclePolicy.CanSubmitForReview(_liveCadState);

            return _cadOperationContext?.AvailableActions?.Any(a => a.Kind == kind && a.IsAvailable) == true;
        }

        private bool CanExecuteCadBusinessAction(CadBusinessActionKind kind)
        {
            return !IsOpeningInIronCad
                && !string.IsNullOrWhiteSpace(_liveCadId)
                && HasCadAction(kind);
        }

        private async void ExecuteCadBusinessActionAsync(CadBusinessActionKind kind)
        {
            var cadClient = MainViewModel.SharedArasCadClient;
            if (cadClient == null || string.IsNullOrWhiteSpace(_liveCadId))
            {
                StatusMessage = "No live CAD is available for this action.";
                return;
            }

            var confirmMessage = kind switch
            {
                CadBusinessActionKind.StartDetailedDesign => "Move this CAD from 'Khoi tao' to 'Thiet ke chi tiet'?",
                CadBusinessActionKind.SubmitForReview => "Submit this CAD for review?",
                CadBusinessActionKind.Approve => "Approve this CAD?",
                CadBusinessActionKind.RequestRework => "Request rework on this CAD?",
                _ => $"Execute {kind}?"
            };

            var confirmResult = System.Windows.MessageBox.Show(
                confirmMessage,
                "Workflow Action",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (confirmResult != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsOpeningInIronCad = true;
                StatusMessage = $"Executing {kind}...";

                var freshContext = await cadClient.GetCadOperationContextAsync(_liveCadId, CancellationToken.None);
                if (freshContext == null)
                {
                    throw new InvalidOperationException("Cannot load fresh CAD workflow context.");
                }

                SetCadOperationContext(freshContext);

                var action = freshContext.AvailableActions?.FirstOrDefault(a => a.Kind == kind && a.IsAvailable);
                if (action == null &&
                    (kind == CadBusinessActionKind.StartDetailedDesign || kind == CadBusinessActionKind.SubmitForReview))
                {
                    action = new CadBusinessAction(
                        kind,
                        kind == CadBusinessActionKind.StartDetailedDesign ? "Start Detailed Design" : "Submit for Review",
                        true,
                        null,
                        true,
                        null,
                        null);
                }

                if (action == null)
                {
                    throw new InvalidOperationException($"Action '{kind}' is not available for the selected CAD.");
                }

                var request = new ExecuteCadBusinessActionRequest(
                    _liveCadId,
                    kind,
                    freshContext.ModifiedOn,
                    action.WorkflowTaskId,
                    action.WorkflowPathId,
                    comment: null);

                var updatedContext = await cadClient.ExecuteCadBusinessActionAsync(request, CancellationToken.None);
                SetCadOperationContext(updatedContext);
                await RefreshCadStateAsync();
                StatusMessage = $"{kind} completed successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{kind} failed: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"{kind} failed: {ex.Message}",
                    "Workflow Action",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                await RefreshCadStateAsync();
            }
            finally
            {
                IsOpeningInIronCad = false;
                _startDetailedDesignCommand.RaiseCanExecuteChanged();
                _submitForReviewCommand.RaiseCanExecuteChanged();
                _approveCadCommand.RaiseCanExecuteChanged();
                _requestReworkCommand.RaiseCanExecuteChanged();
            }
        }

        private async void CheckInAsync()
        {
            var cadClient = MainViewModel.SharedArasCadClient;
            if (cadClient == null)
            {
                StatusMessage = "Not connected to Aras.";
                return;
            }

            var manifest = _workspaceService.LoadManifest(FolderPath);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.LockToken) || string.IsNullOrWhiteSpace(manifest.CadId))
            {
                StatusMessage = "No active checkout session.";
                return;
            }

            if (_checkoutService == null)
            {
                _checkoutService = new CheckoutService(cadClient, _workspaceService);
            }

            IsOpeningInIronCad = true;
            try
            {
                var filePath = manifest.LocalFilePath;
                if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                {
                    StatusMessage = "Local file not found. Save the file in IronCAD first.";
                    return;
                }

                var result = await _checkoutService.UploadAndCheckinAsync(
                    manifest.CadId, manifest.LockToken, filePath, null, CancellationToken.None);

                if (result.Success)
                {
                    _workspaceService.ClearManifest(FolderPath);
                    await RefreshCadStateAsync();
                    StatusMessage = "Check-in completed.";
                }
                else
                {
                    StatusMessage = "Check-in failed: " + (result.ErrorMessage ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Check-in failed: " + ex.Message;
            }
            finally
            {
                IsOpeningInIronCad = false;
            }
        }

        private async void CancelCheckoutAsync()
        {
            var cadClient = MainViewModel.SharedArasCadClient;
            if (cadClient == null)
            {
                StatusMessage = "Not connected to Aras.";
                return;
            }

            var manifest = _workspaceService.LoadManifest(FolderPath);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.CadId))
            {
                StatusMessage = "No active checkout to cancel.";
                return;
            }

            if (_checkoutService == null)
            {
                _checkoutService = new CheckoutService(cadClient, _workspaceService);
            }

            IsOpeningInIronCad = true;
            try
            {
                var success = await _checkoutService.CancelCheckoutAsync(manifest.CadId, CancellationToken.None);
                if (success)
                {
                    _workspaceService.ClearManifest(FolderPath);
                    await RefreshCadStateAsync();
                    StatusMessage = "Checkout cancelled.";
                }
                else
                {
                    _workspaceService.ClearManifest(FolderPath);
                    await RefreshCadStateAsync();
                    StatusMessage = "Checkout cancelled (lock already released on server).";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Cancel checkout failed: " + ex.Message;
            }
            finally
            {
                IsOpeningInIronCad = false;
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

            // The sample/business package order is the source of truth here:
            // 01-01, 01-02, 02-01, 02-02, 03-01, 03-02
            // should map to detail files 001..006 in that same order.
            for (var i = 0; i < businessDetails.Count && i < detailFiles.Count; i++)
            {
                var businessDetail = businessDetails[i];
                var matchedDetail = detailFiles[i];

                if (string.IsNullOrWhiteSpace(businessDetail?.SourceFileName) || matchedDetail == null)
                {
                    continue;
                }

                map[businessDetail.SourceFileName] = matchedDetail;
            }

            return map;
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
                        "{0} is valid for naming policy {1}. PDM structure shows {2} groups and {3} child components; CAD structure is shown separately from {4}.",
                        analysis.ProjectCode ?? "Folder",
                        NamingPolicyVersion,
                        businessStructure.RootNodes.Count,
                        componentCount,
                        Path.GetFileName(sources.CadFolder ?? FolderPath));
                }
                else
                {
                    AnalysisSummary = string.Format(
                        "{0} is valid for naming policy {1}. CAD structure includes {2} assembly file(s) and {3} component file(s).",
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

            OnPropertyChanged(nameof(PushPreviewReadiness));
            OnPropertyChanged(nameof(CanPush));
            _pushCommand.RaiseCanExecuteChanged();
        }

        private async Task RefreshPreviewFromServerAsync()
        {
            if (_pushPreview == null)
                return;

            var client = MainViewModel.SharedPdmClient;
            if (client == null)
                return;

            var request = BuildPushRequest();
            if (request == null)
                return;

            var refreshVersion = Interlocked.Increment(ref _previewRefreshVersion);

            try
            {
                var existence = await client.PreviewExistenceAsync(request, CancellationToken.None);
                if (existence == null)
                    return;

                if (refreshVersion != _previewRefreshVersion)
                    return;

                PreviewParts.Clear();
                foreach (var part in _pushPreview.Parts)
                {
                    var exists = existence.PartsByNumber.TryGetValue(part.PartNumber, out var e) && e;
                    var action = exists ? "Reuse" : "Create";
                    if (exists &&
                        !string.IsNullOrWhiteSpace(part.ParentLogicalCode) &&
                        existence.BomByChildLogicalCode != null &&
                        existence.BomByChildLogicalCode.TryGetValue(part.LogicalCode, out var bomInfo))
                    {
                        if (!bomInfo.Exists)
                        {
                            action = "Update BOM";
                        }
                        else if (bomInfo.ExistingQuantity.HasValue && bomInfo.ExistingQuantity.Value != part.Quantity)
                        {
                            action = "Update Qty";
                        }
                    }

                    PreviewParts.Add(new PartPreviewRow
                    {
                        LogicalCode = part.LogicalCode,
                        ParentLogicalCode = part.ParentLogicalCode,
                        PartNumber = part.PartNumber,
                        Name = part.Name,
                        Classification = part.Classification,
                        Quantity = part.Quantity,
                        Action = action
                    });
                }

                PreviewCads.Clear();
                foreach (var cad in _pushPreview.Cads)
                {
                    var exists = existence.CadsByNumber.TryGetValue(cad.CadNumber, out var e) && e;
                    PreviewCads.Add(new CadPreviewRow
                    {
                        SourceFileName = cad.SourceFileName,
                        LogicalCode = cad.LogicalCode,
                        CadNumber = cad.CadNumber,
                        Classification = cad.Classification,
                        Action = exists ? "Reuse" : "Create",
                        LinkedPartLogicalCode = cad.LinkedPartLogicalCode
                    });
                }

                PreviewDocuments.Clear();
                foreach (var doc in _pushPreview.Documents)
                {
                    var exists = existence.DocumentsByNumber.TryGetValue(doc.DocumentNumber, out var e) && e;
                    PreviewDocuments.Add(new DocumentPreviewRow
                    {
                        SourceFileName = doc.SourceFileName,
                        LogicalCode = doc.LogicalCode,
                        DocumentNumber = doc.DocumentNumber,
                        Classification = doc.Classification,
                        LinkTargetType = doc.LinkTargetType,
                        Action = exists ? "Reuse" : "Create",
                        LinkedPartLogicalCode = doc.LinkedPartLogicalCode
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Could not compare preview with Aras. Showing local preview only. " + ex.Message;
            }
        }

        private void RefreshPushPreview()
        {
            if (_latestAnalysis == null)
            {
                return;
            }

            BuildPushPreview(_latestAnalysis, _latestBusinessStructure);
            _ = RefreshPreviewFromServerAsync();
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

            if (Directory.GetFiles(folderPath, "*.ics", SearchOption.TopDirectoryOnly).Length > 0)
            {
                result.CadFolder = folderPath;
                result.PackageFolder = folderPath;
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
            string perspective = null,
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
            Perspective = perspective ?? "PDM";
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
        public string Perspective { get; }
        public string PrimaryCad { get; }
        public string LockedBy { get; }
        public string SourceDocument { get; }
        public ObservableCollection<PdmStructureNode> Children { get; }
    }

    public sealed class PdmStructureMappingItem
    {
        public PdmStructureMappingItem(string pdmCode, string pdmName, string nodeType, string mappedCad, string status)
        {
            PdmCode = pdmCode;
            PdmName = pdmName;
            NodeType = nodeType;
            MappedCad = mappedCad;
            Status = status;
        }

        public string PdmCode { get; }
        public string PdmName { get; }
        public string NodeType { get; }
        public string MappedCad { get; }
        public string Status { get; }
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

