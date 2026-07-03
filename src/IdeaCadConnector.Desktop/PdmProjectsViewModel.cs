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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Desktop.Services;
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
        private WorkspaceCommit _selectedCommit;
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
        private WorkspaceLibraryReferenceStore _libraryReferenceStore;
        private IRevisionService _revisionService;
        private PdmRevisePreconditionResult _revisionPreconditions;
        private string _cadLockStateText;
        private string _lockedByText;
        private string _cadFileStateText;
        private string _cadRevisionText;
        private string _cadGenerationText;
        private string _cadLifecycleText;
        private string _cadEditPolicyText;
        private string _cadDriftText;
        private string _cadRevisionReadinessText;
        private string _liveCadId;
        private string _liveCadState;
        private string _liveCadRevision;
        private int _liveCadGeneration;
        private bool _liveHasNativeFile;
        private string _livePartId;
        private Dictionary<string, string> _postPushPartIds;
        private bool _isCheckedOutByMe;
        private bool _isCheckedOutByOther;
        private bool _isAvailable;
        private bool _canCheckIn;
        private bool _canCancelCheckout;
        private CadOperationContext _cadOperationContext;
        private IReadOnlyList<WorkspaceLibraryReference> _workspaceLibraryReferences = Array.Empty<WorkspaceLibraryReference>();

        public PdmProjectsViewModel()
            : this(new GuidanceRevisionService())
        {
        }

        public PdmProjectsViewModel(IRevisionService revisionService)
        {
            _workspaceService = new WorkspaceService(new WorkspaceOptions());
            _libraryReferenceStore = new WorkspaceLibraryReferenceStore(_workspaceService);
            _revisionService = revisionService ?? throw new ArgumentNullException(nameof(revisionService));
            Repositories = new ObservableCollection<string>();
            Branches = new ObservableCollection<string>();
            WorkspaceCommits = new ObservableCollection<WorkspaceCommit>();
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

            FolderPath = GetDefaultSampleFolder();
            LoadBranchesForFolder();
            AnalysisSummary = Loc(TranslationKeys.PdmSelectFolderHint);
            StatusMessage = Loc(TranslationKeys.PdmProjectsTitle);
            RefreshConnectionStatus();

            CloneCommand = new RelayCommand(_ => ExecuteCloneAsync());
            PullCommand = new RelayCommand(_ => StatusMessage = Loc(TranslationKeys.PullNotConnected));
            _pushCommand = new RelayCommand(_ => PushWorkspace(), _ => !IsPushing && CanPush);
            PushCommand = _pushCommand;
            NewBranchCommand = new RelayCommand(_ => ExecuteNewBranchAsync());
            _refreshCommand = new RelayCommand(_ => AnalyzeFolder());
            RefreshCommand = _refreshCommand;
            _analyzeFolderCommand = new RelayCommand(_ => AnalyzeFolder());
            AnalyzeFolderCommand = _analyzeFolderCommand;
            _browseFolderCommand = new RelayCommand(_ => BrowseFolder());
            BrowseFolderCommand = _browseFolderCommand;
            OpenDocumentCommand = new RelayCommand(doc => OpenDocument(doc as PdmDocumentItem), doc => doc is PdmDocumentItem);
            OpenInIronCadCommand = new RelayCommand(_ => OpenInIronCadAsync(), _ => CanOpenInIronCad);
            CheckInCommand = new RelayCommand(_ => CheckInAsync(), _ => CanCheckIn);
            CancelCheckoutCommand = new RelayCommand(_ => CancelCheckoutAsync(), _ => CanCancelCheckout);
            _startDetailedDesignCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.StartDetailedDesign), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.StartDetailedDesign));
            StartDetailedDesignCommand = _startDetailedDesignCommand;
            _submitForReviewCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.SubmitForReview), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.SubmitForReview));
            SubmitForReviewCommand = _submitForReviewCommand;
            _approveCadCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.Approve), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            ApproveCadCommand = _approveCadCommand;
            _requestReworkCommand = new RelayCommand(_ => ExecuteCadBusinessActionAsync(CadBusinessActionKind.RequestRework), _ => CanExecuteCadBusinessAction(CadBusinessActionKind.RequestRework));
            RequestReworkCommand = _requestReworkCommand;
            StartNewRevisionCommand = new RelayCommand(_ => ExecuteStartNewRevisionAsync(), _ => CanStartNewRevision);
            CommitCommand = new RelayCommand(_ => _ = ExecuteCommitAsync(), _ => CanCommit);
            ToggleCadSectionCommand = new RelayCommand(_ => ToggleCadSection());
            ToggleDocumentsSectionCommand = new RelayCommand(_ => ToggleDocumentsSection());
            SaveSelectedNodeToLibraryCommand = new RelayCommand(_ => SaveSelectedNodeToLibraryAsync(), _ => CanSaveSelectedNodeToLibrary);
            RemoveSelectedLibraryReferenceCommand = new RelayCommand(_ => RemoveSelectedLibraryReferenceAsync(), _ => HasRemoveLibraryReferenceAction);


            AnalyzeFolder();
        }

        public string RepositoryCodeForDisplay => SelectedRepository ?? _latestAnalysis?.ProjectCode ?? "-";

        public ObservableCollection<string> Repositories { get; }
        public ObservableCollection<string> Branches { get; }
        public ObservableCollection<WorkspaceCommit> WorkspaceCommits { get; }
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
                    OnPropertyChanged(nameof(BranchPushAllowed));
                    OnPropertyChanged(nameof(BranchStatusText));
                    LoadCommitHistoryForFolder();
                    OnPropertyChanged(nameof(LatestCommitSummary));
                    OnPropertyChanged(nameof(HasUncommittedChanges));
                    OnPropertyChanged(nameof(CanCommit));
                    RefreshPushPreview();
                }
            }
        }

        public WorkspaceCommit SelectedCommit
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
        public bool BranchPushAllowed => IsMainBranch;
        public string BranchStatusText => IsMainBranch
            ? "Live branch (push allowed)"
            : "Preview branch (no live push)";

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
                    OnPropertyChanged(nameof(HasOpenInIronCadAction));
                    OnPropertyChanged(nameof(HasSaveToLibraryAction));
                    OnPropertyChanged(nameof(HasRemoveLibraryReferenceAction));
                    OnPropertyChanged(nameof(CanSaveSelectedNodeToLibrary));
                    ((RelayCommand)OpenInIronCadCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)SaveSelectedNodeToLibraryCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RemoveSelectedLibraryReferenceCommand).RaiseCanExecuteChanged();
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
            set
            {
                if (SetField(ref _isAnalyzing, value))
                {
                    OnPropertyChanged(nameof(CanSaveSelectedNodeToLibrary));
                    (SaveSelectedNodeToLibraryCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(HasRemoveLibraryReferenceAction));
                    (RemoveSelectedLibraryReferenceCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsPushing
        {
            get => _isPushing;
            set
            {
                if (SetField(ref _isPushing, value))
                {
                    _pushCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(CanSaveSelectedNodeToLibrary));
                    (SaveSelectedNodeToLibraryCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(HasRemoveLibraryReferenceAction));
                    (RemoveSelectedLibraryReferenceCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
                    OnPropertyChanged(nameof(HasOpenInIronCadAction));
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

        // TODO(PERF-REVISION-SEAM): Wire real server path when PDM schema
        private async void ExecuteStartNewRevisionAsync()
        {
            var request = new PdmReviseRequest
            {
                CadId = _liveCadId,
                CadNumber = SelectedNode?.PrimaryCad ?? "-",
                PartId = _livePartId,
                PartNumber = SelectedNode?.PartCode ?? "-"
            };
            var result = await _revisionService.ReviseAsync(request, CancellationToken.None);
            if (result.Success)
            {
                StatusMessage = $"New revision created: {result.NewRevision ?? "-"}";
                // Clear the old manifest — the checkout session pointed to the released CAD
                // and is no longer valid after revision.
                _workspaceService.ClearManifest(FolderPath);
                await RefreshCadStateAsync(result.NewCadId, result.NewPartId);
                if (string.IsNullOrWhiteSpace(_liveCadRevision) && !string.IsNullOrWhiteSpace(result.NewRevision))
                    _liveCadRevision = result.NewRevision;
                if (string.IsNullOrWhiteSpace(_liveCadState) && !string.IsNullOrWhiteSpace(result.NewLifecycleState))
                    _liveCadState = result.NewLifecycleState;
                if (!string.IsNullOrWhiteSpace(result.NewCadId))
                    _liveCadId = result.NewCadId;
                if (!string.IsNullOrWhiteSpace(result.NewPartId))
                    _livePartId = result.NewPartId;
                UpdateCadUiState();
                await RefreshRevisionPreconditionsAsync();
                RefreshCanOpenInIronCad();
            }
            else if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                StatusMessage = result.ErrorMessage;
            }
        }

        public bool CanOpenInIronCad =>
            !IsOpeningInIronCad &&
            SelectedNode != null &&
            !IsSelectedRootAssemblyNode() &&
            !string.IsNullOrWhiteSpace(SelectedNode.PrimaryCad) &&
            SelectedNode.PrimaryCad != "-" &&
            MainViewModel.SharedArasCadClient != null &&
            !string.IsNullOrWhiteSpace(_liveCadId) &&
            (_liveHasNativeFile || CadLifecyclePolicy.CanCheckout(_liveCadState));

        public bool CanStartNewRevision =>
            SelectedNode != null &&
            (_revisionPreconditions?.CanRevise ?? false) &&
            MainViewModel.SharedArasCadClient != null;

        public string OpenInIronCadModeText =>
            IsReleasedCad() ? "Open in IronCAD (read-only)" : "Open in IronCAD";

        public string CommitMessage
        {
            get => _commitMessage;
            set
            {
                if (SetField(ref _commitMessage, value))
                {
                    OnPropertyChanged(nameof(CanCommit));
                    RefreshPushPreview();
                }
            }
        }

        public ICommand CommitCommand { get; private set; }

        public bool CanCommit =>
            !string.IsNullOrWhiteSpace(FolderPath) &&
            !string.IsNullOrWhiteSpace(CommitMessage) &&
            HasUncommittedChanges;

        public bool HasUncommittedChanges
        {
            get
            {
                var sig = ComputeSnapshotSignature();
                if (sig == null)
                    return false;
                var last = GetLatestCommitForBranch();
                return last == null || last.SnapshotSignature != sig;
            }
        }

        public string LatestCommitSummary
        {
            get
            {
                var last = GetLatestCommitForBranch();
                if (last == null)
                {
                    var history = _workspaceService.LoadCommitHistory(FolderPath);
                    if (history?.Commits == null || history.Commits.Count == 0)
                        return "No local commits yet";
                    return "No local commits on this branch yet";
                }
                return "[local] " + last.Message + " (" + last.Timestamp.ToString("g") + ")";
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
        public ICommand StartNewRevisionCommand { get; }
        public ICommand ToggleCadSectionCommand { get; }
        public ICommand ToggleDocumentsSectionCommand { get; }
        public ICommand SaveSelectedNodeToLibraryCommand { get; }
        public ICommand RemoveSelectedLibraryReferenceCommand { get; }

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

        public string CadDriftText
        {
            get => _cadDriftText;
            set
            {
                if (SetField(ref _cadDriftText, value))
                    OnPropertyChanged(nameof(HasCadDrift));
            }
        }

        public bool HasCadDrift => !string.IsNullOrWhiteSpace(_cadDriftText);

        public string CadRevisionReadinessText
        {
            get => _cadRevisionReadinessText ?? string.Empty;
            set
            {
                if (SetField(ref _cadRevisionReadinessText, value))
                {
                    OnPropertyChanged(nameof(HasCadRevisionReadinessInfo));
                    OnPropertyChanged(nameof(HasCadRevisionEntryPoint));
                }
            }
        }

        public bool HasCadRevisionReadinessInfo => !string.IsNullOrWhiteSpace(_cadRevisionReadinessText);

        public bool HasCadRevisionEntryPoint =>
            GuidanceRevisionService.ShouldShowRevisionEntryPoint(_liveCadId, CadRevisionReadinessText);

        public bool HasOpenInIronCadAction =>
            SelectedNode != null &&
            !IsSelectedRootAssemblyNode() &&
            !string.IsNullOrWhiteSpace(SelectedNode.PrimaryCad) &&
            SelectedNode.PrimaryCad != "-" &&
            MainViewModel.SharedArasCadClient != null &&
            !string.IsNullOrWhiteSpace(_liveCadId) &&
            (_liveHasNativeFile || CadLifecyclePolicy.CanCheckout(_liveCadState));

        public bool HasCheckInCadAction => IsCheckedOutByMe || CanCheckIn;

        public bool HasCancelCheckoutCadAction => IsCheckedOutByMe || CanCancelCheckout;

        public bool HasSaveToLibraryAction => SelectedNode != null;

        public bool HasRemoveLibraryReferenceAction =>
            SelectedNode != null &&
            SelectedNode.IsLibraryReference &&
            !IsAnalyzing &&
            !IsPushing;

        public bool CanSaveSelectedNodeToLibrary =>
            !IsAnalyzing &&
            !IsPushing &&
            SelectedNode != null &&
            MainViewModel.SharedPartLibraryClient != null;

        public bool IsCheckedOutByMe
        {
            get => _isCheckedOutByMe;
            set
            {
                if (SetField(ref _isCheckedOutByMe, value))
                {
                    OnPropertyChanged(nameof(HasCheckInCadAction));
                    OnPropertyChanged(nameof(HasCancelCheckoutCadAction));
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

        public bool CanCheckIn
        {
            get => _canCheckIn;
            set
            {
                if (SetField(ref _canCheckIn, value))
                {
                    OnPropertyChanged(nameof(HasCheckInCadAction));
                    ((RelayCommand)CheckInCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanCancelCheckout
        {
            get => _canCancelCheckout;
            set
            {
                if (SetField(ref _canCancelCheckout, value))
                {
                    OnPropertyChanged(nameof(HasCancelCheckoutCadAction));
                    ((RelayCommand)CancelCheckoutCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string WorkflowStatusText
        {
            get
            {
                if (_cadOperationContext?.ActiveTask == null)
                {
                    return LifecycleDisplayText.GetWorkflowIdleText(_liveCadState);
                }

                var paths = _cadOperationContext.ActiveTask.AvailablePaths;
                var openPaths = paths?.Count(p => !p.IsComplete) ?? 0;
                return string.Format(Loc(TranslationKeys.TaskActionsAvailable), _cadOperationContext.ActiveTask.ActivityName, openPaths);
            }
        }

        public bool HasAnyCadBusinessAction =>
            HasCadAction(CadBusinessActionKind.StartDetailedDesign) ||
            HasCadAction(CadBusinessActionKind.SubmitForReview) ||
            HasCadAction(CadBusinessActionKind.Approve) ||
            HasCadAction(CadBusinessActionKind.RequestRework);

        public bool HasStartDetailedDesignBusinessAction => HasCadAction(CadBusinessActionKind.StartDetailedDesign);

        public bool HasSubmitForReviewBusinessAction => HasCadAction(CadBusinessActionKind.SubmitForReview);

        public bool HasApproveBusinessAction => HasCadAction(CadBusinessActionKind.Approve);

        public bool HasRequestReworkBusinessAction => HasCadAction(CadBusinessActionKind.RequestRework);

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
                StatusMessage = Loc(TranslationKeys.StatusPushBlocked);
                return;
            }

            var client = MainViewModel.SharedPdmClient;
            if (client == null)
            {
                StatusMessage = Loc(TranslationKeys.StatusNotConnected);
                return;
            }

            IsPushing = true;
            StatusMessage = Loc(TranslationKeys.StatusPushing);

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
                await RecordLibraryUsageAfterPushAsync(result).ConfigureAwait(true);
            }
            else
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusPushFailedBare), result.ErrorMessage ?? Loc(TranslationKeys.UnknownError));
            }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusPushFailedBare), ex.Message);
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
                    Quantity = p.Quantity,
                    ExistingPartId = p.ExistingPartId,
                    ExistingPartConfigId = p.ExistingPartConfigId,
                    ExistingPartRevision = p.ExistingPartRevision,
                    SourceKind = p.SourceKind,
                    LibraryEntryId = p.LibraryEntryId,
                    RevisionPolicy = p.RevisionPolicy,
                    IsExternalReference = p.IsExternalReference
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
                var idLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Build SourceKey → result lookup for LogicalCode-based matching
                var resultBySourceKey = new Dictionary<string, PdmItemResult>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in result.PartResults)
                {
                    if (!string.IsNullOrWhiteSpace(r.SourceKey) && !resultBySourceKey.ContainsKey(r.SourceKey))
                        resultBySourceKey[r.SourceKey] = r;
                }

                foreach (var row in PreviewParts)
                {
                    var logicalCode = row.LogicalCode;
                    var res = (!string.IsNullOrWhiteSpace(logicalCode) && resultBySourceKey.TryGetValue(logicalCode, out var matched))
                        ? matched
                        : null;

                    if (res != null)
                    {
                        if (res.Success)
                            row.Action = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
                        else
                            row.Action = "Failed: " + (res.ErrorMessage ?? "Unknown");

                        if (res.Success && !string.IsNullOrWhiteSpace(res.ArasId) && !string.IsNullOrWhiteSpace(logicalCode))
                            idLookup[logicalCode] = res.ArasId;
                    }
                }

                _postPushPartIds = idLookup;

                // Populate _livePartId from root part (no parent) so Save to Library works immediately after push
                if (_pushPreview?.Parts != null && string.IsNullOrWhiteSpace(_livePartId))
                {
                    foreach (var part in _pushPreview.Parts)
                    {
                        if (string.IsNullOrWhiteSpace(part.ParentLogicalCode))
                        {
                            var res = !string.IsNullOrWhiteSpace(part.LogicalCode) && resultBySourceKey.TryGetValue(part.LogicalCode, out var matched)
                                ? matched
                                : null;
                            if (res != null && res.Success && !string.IsNullOrWhiteSpace(res.ArasId))
                            {
                                _livePartId = res.ArasId;
                                break;
                            }
                        }
                    }
                }

                RefreshCanOpenInIronCad();
            }

            if (result.CadResults != null)
            {
                var cadResultByKey = new Dictionary<string, PdmItemResult>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in result.CadResults)
                {
                    if (!string.IsNullOrWhiteSpace(r.SourceKey) && !cadResultByKey.ContainsKey(r.SourceKey))
                        cadResultByKey[r.SourceKey] = r;
                }

                foreach (var row in PreviewCads)
                {
                    var sourceFileName = row.SourceFileName;
                    var res = (!string.IsNullOrWhiteSpace(sourceFileName) && cadResultByKey.TryGetValue(sourceFileName, out var matched))
                        ? matched
                        : null;

                    if (res != null)
                    {
                        if (res.Success)
                            row.Action = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
                        else if (!string.IsNullOrWhiteSpace(res.ArasId))
                        {
                            var metaAction = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
                            row.Action = metaAction + " (file failed): " + (res.ErrorMessage ?? "Unknown");
                        }
                        else
                            row.Action = "Failed: " + (res.ErrorMessage ?? "Unknown");
                    }
                }
            }

            if (result.DocumentResults != null)
            {
                var docResultByKey = new Dictionary<string, PdmItemResult>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in result.DocumentResults)
                {
                    if (!string.IsNullOrWhiteSpace(r.SourceKey) && !docResultByKey.ContainsKey(r.SourceKey))
                        docResultByKey[r.SourceKey] = r;
                }

                foreach (var row in PreviewDocuments)
                {
                    var sourceFileName = row.SourceFileName;
                    var res = (!string.IsNullOrWhiteSpace(sourceFileName) && docResultByKey.TryGetValue(sourceFileName, out var matched))
                        ? matched
                        : null;

                    if (res != null)
                    {
                        if (res.Success)
                            row.Action = string.IsNullOrWhiteSpace(res.ActionTaken) ? "Created" : res.ActionTaken;
                        else
                            row.Action = "Failed: " + (res.ErrorMessage ?? "Unknown");
                    }
                }
            }
        }

        private async Task RecordLibraryUsageAfterPushAsync(PdmPushResult result)
        {
            if (result == null ||
                !result.Success ||
                result.StagingOnly ||
                _pushPreview?.Parts == null ||
                result.PartResults == null)
            {
                return;
            }

            var partLibraryClient = MainViewModel.SharedPartLibraryClient;
            if (partLibraryClient == null)
            {
                return;
            }

            var resultBySourceKey = new Dictionary<string, PdmItemResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in result.PartResults)
            {
                if (!string.IsNullOrWhiteSpace(r.SourceKey) && !resultBySourceKey.ContainsKey(r.SourceKey))
                    resultBySourceKey[r.SourceKey] = r;
            }

            var projectCode = _latestAnalysis?.ProjectCode ?? RepositoryCodeForDisplay;
            var commitId = result.CommitId ?? string.Empty;

            foreach (var previewPart in _pushPreview.Parts)
            {
                if (!previewPart.IsExternalReference ||
                    !string.Equals(previewPart.SourceKind, LibrarySourceKind.LibraryReference.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(previewPart.LibraryEntryId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(previewPart.ParentLogicalCode))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(previewPart.LogicalCode))
                {
                    continue;
                }

                if (!resultBySourceKey.TryGetValue(previewPart.LogicalCode, out var pushPart) ||
                    !pushPart.Success ||
                    string.IsNullOrWhiteSpace(pushPart.ArasId))
                {
                    continue;
                }

                if (_postPushPartIds == null ||
                    !_postPushPartIds.TryGetValue(previewPart.ParentLogicalCode, out var parentPartId) ||
                    string.IsNullOrWhiteSpace(parentPartId))
                {
                    continue;
                }

                try
                {
                    await partLibraryClient.RecordUsageAsync(new LibraryUsageRequest
                    {
                        LibraryEntryId = previewPart.LibraryEntryId,
                        PartId = pushPart.ArasId,
                        ProjectCode = projectCode,
                        ParentPartId = parentPartId,
                        Quantity = Math.Max(1, previewPart.Quantity),
                        UsedBy = MainViewModel.SharedUserName ?? "engineer",
                        CommitId = commitId,
                        ActionType = pushPart.ActionTaken ?? "ReusedFromLibrary"
                    }, CancellationToken.None).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Library usage record skipped: " + ex.Message);
                }
            }
        }

        private async void ExecuteCloneAsync()
        {
            RefreshConnectionStatus();

            var client = MainViewModel.SharedPdmClient;
            if (client == null)
            {
                StatusMessage = Loc(TranslationKeys.StatusNotConnected);
                return;
            }

            var repositoryCode = (SelectedRepository ?? _latestAnalysis?.ProjectCode)?.Trim();
            if (string.IsNullOrWhiteSpace(repositoryCode))
            {
                StatusMessage = Loc(TranslationKeys.StatusEnterRepoCode);
                return;
            }

            var targetFolder = FolderPath?.Trim();
            if (string.IsNullOrWhiteSpace(targetFolder))
            {
                StatusMessage = Loc(TranslationKeys.StatusChooseFolder);
                return;
            }

            StatusMessage = Loc(TranslationKeys.StatusCloning);

            try
            {
                var selectedBranch = SelectedBranch ?? "main";
                var result = await client.CloneLatestToWorkspaceAsync(new PdmCloneRequest
                {
                    RepositoryCode = repositoryCode,
                    TargetFolder = targetFolder,
                    BranchName = selectedBranch
                }, CancellationToken.None);

                if (!result.Success)
                {
                    StatusMessage = string.Format(Loc(TranslationKeys.StatusCloneFailedBare), result.ErrorMessage ?? Loc(TranslationKeys.UnknownError));
                    return;
                }

                FolderPath = result.ResolvedProjectFolder ?? targetFolder;
                _workspaceService.ClearManifest(FolderPath);
                _workspaceService.EnsureMainBranch(FolderPath);
                EnsureLocalBranchExists(FolderPath, selectedBranch);
                LoadBranchesForFolder();
                if (Branches.Contains(selectedBranch))
                {
                    SelectedBranch = selectedBranch;
                }

                AnalyzeFolder();

                var cloneSummary = string.Format(
                    "Clone complete. Downloaded {0} CAD file(s) and created {1} document placeholder(s) in {2}.",
                    result.DownloadedCadFileCount,
                    result.PlaceholderDocumentCount,
                    FolderPath);

                if (result.Warnings?.Count > 0)
                {
                    cloneSummary += " Warning: " + string.Join("; ", result.Warnings);
                }

                StatusMessage = cloneSummary;
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusCloneFailedBare), ex.Message);
            }
        }

        private void AnalyzeFolder()
        {
            IsAnalyzing = true;
            try
            {
                LoadBranchesForFolder();
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

                try
                {
                    _workspaceLibraryReferences = LoadWorkspaceLibraryReferences();
                }
                catch (InvalidOperationException ex)
                {
                    var path = _libraryReferenceStore?.GetFilePath(FolderPath) ?? ".idea-pdm/library-references.json";
                    _workspaceLibraryReferences = Array.Empty<WorkspaceLibraryReference>();
                    _latestAnalysis.Issues.Add(new PdmNamingIssue
                    {
                        FileName = path,
                        Message = "Library references file is corrupted: " + ex.Message,
                        BlocksPush = true
                    });
                    StatusMessage = "Library references file is corrupted. Open '" + path + "' and fix the JSON format, or delete the file to start fresh.";
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

                if (!string.IsNullOrWhiteSpace(_latestAnalysis.ProjectCode))
                {
                    Repositories.Add(_latestAnalysis.ProjectCode);
                    SelectedRepository = _latestAnalysis.ProjectCode;
                }
                else
                {
                    SelectedRepository = null;
                }

                BuildPdmStructure(_latestAnalysis, _latestBusinessStructure);
                TrackedFileCount = _latestAnalysis.TrackedFiles.Count;
                IgnoredFileCount = _latestAnalysis.IgnoredFiles.Count;
                BlockingIssueCount = _latestAnalysis.Issues.Count(issue => issue.BlocksPush);
                TotalChangeCount = TrackedFileCount + IgnoredFileCount + BlockingIssueCount;
                BuildNamingPreview(_latestAnalysis);
                BuildChanges(_latestAnalysis);
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
                LoadCommitHistoryForFolder();

                OnPropertyChanged(nameof(HasPushPreview));
                OnPropertyChanged(nameof(PushPreviewReadiness));
                OnPropertyChanged(nameof(CanPush));
                OnPropertyChanged(nameof(WorkingTreeSummary));
                OnPropertyChanged(nameof(HasUncommittedChanges));
                OnPropertyChanged(nameof(CanCommit));
                OnPropertyChanged(nameof(LatestCommitSummary));

                _pushCommand.RaiseCanExecuteChanged();
                (CommitCommand as RelayCommand)?.RaiseCanExecuteChanged();
                _refreshCommand.RaiseCanExecuteChanged();
                _analyzeFolderCommand.RaiseCanExecuteChanged();
                _browseFolderCommand.RaiseCanExecuteChanged();
            }
            finally
            {
                IsAnalyzing = false;
            }
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

            foreach (var reference in _workspaceLibraryReferences.OrderBy(item => item.PartNumber, StringComparer.OrdinalIgnoreCase))
            {
                Changes.Add(new PdmFileChange("Library", reference.PartNumber + " -> " + reference.ParentLogicalCode, "#FF0F8F86"));
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

            MergeLibraryReferencesIntoStructure(root, analysis?.Issues);

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

        private IReadOnlyList<WorkspaceLibraryReference> LoadWorkspaceLibraryReferences()
        {
            return _libraryReferenceStore.Load(FolderPath)
                .Where(reference =>
                    !string.IsNullOrWhiteSpace(reference.ReferenceId) &&
                    !string.IsNullOrWhiteSpace(reference.ParentLogicalCode) &&
                    !string.IsNullOrWhiteSpace(reference.PartId))
                .ToList();
        }

        private void MergeLibraryReferencesIntoStructure(PdmStructureNode root)
        {
            if (root == null || _workspaceLibraryReferences == null || _workspaceLibraryReferences.Count == 0)
                return;

            foreach (var reference in _workspaceLibraryReferences)
            {
                var parent = FindStructureNode(root, reference.ParentLogicalCode);
                if (parent == null)
                    continue;

                parent.Children.Add(CreateLibraryStructureNode(reference));
            }
        }

        private void MergeLibraryReferencesIntoStructure(PdmStructureNode root, IList<PdmNamingIssue> analysisIssues)
        {
            if (root == null || _workspaceLibraryReferences == null || _workspaceLibraryReferences.Count == 0)
                return;

            foreach (var reference in _workspaceLibraryReferences)
            {
                var parent = FindStructureNode(root, reference.ParentLogicalCode);
                if (parent != null)
                {
                    parent.Children.Add(CreateLibraryStructureNode(reference));
                    continue;
                }

                root.Children.Add(CreateLibraryStructureNode(reference, true));
                analysisIssues?.Add(new PdmNamingIssue
                {
                    FileName = reference.PartNumber ?? reference.ReferenceId ?? reference.LocalLogicalCode ?? "Library reference",
                    Message = "Library reference parent '" + reference.ParentLogicalCode + "' was not found in the current PDM structure.",
                    BlocksPush = true
                });
            }
        }

        private static PdmStructureNode FindStructureNode(PdmStructureNode node, string partCode)
        {
            if (node == null || string.IsNullOrWhiteSpace(partCode))
                return null;

            if (string.Equals(node.PartCode, partCode, StringComparison.OrdinalIgnoreCase))
                return node;

            foreach (var child in node.Children)
            {
                var match = FindStructureNode(child, partCode);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static PdmStructureNode CreateLibraryStructureNode(WorkspaceLibraryReference reference)
        {
            return new PdmStructureNode(
                (reference.PartName ?? reference.PartNumber ?? "Library Part") + " [Library]",
                reference.LocalLogicalCode ?? reference.PartNumber,
                "Component",
                Math.Max(1, reference.Quantity),
                reference.Revision ?? "-",
                "Library reference • " + (reference.RevisionPolicy ?? "Pinned"),
                "#FF0F8F86",
                perspective: "PDM",
                primaryCad: reference.PartNumber ?? "-",
                sourceDocument: "Library Entry",
                sourceKind: LibrarySourceKind.LibraryReference.ToString(),
                libraryEntryId: reference.LibraryEntryId,
                arasPartId: reference.PartId,
                arasConfigId: reference.PartConfigId,
                revisionPolicy: reference.RevisionPolicy,
                isLibraryReference: true,
                referenceId: reference.ReferenceId);
        }

        private static PdmStructureNode CreateLibraryStructureNode(WorkspaceLibraryReference reference, bool isOrphan)
        {
            var node = CreateLibraryStructureNode(reference);
            if (!isOrphan)
                return node;

            return new PdmStructureNode(
                node.Name,
                node.PartCode,
                node.NodeType,
                node.Quantity,
                node.Revision,
                "Library reference • Missing parent",
                "#FFD54B4B",
                children: node.Children,
                perspective: node.Perspective,
                primaryCad: node.PrimaryCad,
                lockedBy: node.LockedBy,
                sourceDocument: node.SourceDocument,
                sourceKind: node.SourceKind,
                libraryEntryId: node.LibraryEntryId,
                arasPartId: node.ArasPartId,
                arasConfigId: node.ArasConfigId,
                revisionPolicy: node.RevisionPolicy,
                isLibraryReference: node.IsLibraryReference,
                referenceId: node.ReferenceId);
        }

        private AnalyzeResult AppendLibraryReferenceNodes(AnalyzeResult analyzeResult)
        {
            if (analyzeResult == null || _workspaceLibraryReferences == null || _workspaceLibraryReferences.Count == 0)
                return analyzeResult;

            var structureNodes = analyzeResult.StructureNodes?.ToList() ?? new List<AnalyzedStructureNode>();
            var maxSortOrder = structureNodes.Count == 0 ? 0 : structureNodes.Max(node => node.SortOrder);

            foreach (var reference in _workspaceLibraryReferences)
            {
                structureNodes.Add(new AnalyzedStructureNode
                {
                    LogicalCode = reference.LocalLogicalCode,
                    ParentLogicalCode = reference.ParentLogicalCode,
                    DisplayName = reference.PartName ?? reference.PartNumber,
                    NodeType = "Component",
                    PartNumber = reference.PartNumber,
                    Quantity = Math.Max(1, reference.Quantity),
                    SourceDocumentPath = "Library Entry",
                    PrimaryCadPath = null,
                    SortOrder = ++maxSortOrder,
                    SourceKind = LibrarySourceKind.LibraryReference.ToString(),
                    LibraryEntryId = reference.LibraryEntryId,
                    ExistingPartId = reference.PartId,
                    ExistingPartConfigId = reference.PartConfigId,
                    ExistingPartRevision = reference.Revision,
                    RevisionPolicy = reference.RevisionPolicy,
                    IsExternalReference = true
                });
            }

            return new AnalyzeResult
            {
                RepositoryCode = analyzeResult.RepositoryCode,
                ProjectName = analyzeResult.ProjectName,
                PackageSourcePath = analyzeResult.PackageSourcePath,
                CadSourcePath = analyzeResult.CadSourcePath,
                PolicyVersion = analyzeResult.PolicyVersion,
                StructureNodes = structureNodes,
                CadFiles = analyzeResult.CadFiles,
                DocumentFiles = analyzeResult.DocumentFiles,
                IgnoredFiles = analyzeResult.IgnoredFiles,
                Warnings = analyzeResult.Warnings,
                Summary = analyzeResult.Summary
            };
        }

        public IReadOnlyList<LibraryParentCandidate> GetLibraryParentCandidates()
        {
            var candidates = new List<LibraryParentCandidate>();
            foreach (var root in PdmStructure)
                CollectParentCandidates(root, candidates);
            return candidates;
        }

        public LibraryReferenceMutationResult AddLibraryReference(WorkspaceLibraryReference reference)
        {
            if (reference == null)
            {
                return new LibraryReferenceMutationResult(false, "No Library reference was provided.");
            }

            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                return new LibraryReferenceMutationResult(false, "Open or clone a PDM project before adding a Library Part.");
            }

            if (string.IsNullOrWhiteSpace(reference.ParentLogicalCode))
            {
                return new LibraryReferenceMutationResult(false, "Choose a target parent in the Product Structure.");
            }

            if (reference.Quantity <= 0)
            {
                return new LibraryReferenceMutationResult(false, "Quantity must be greater than 0.");
            }

            if (!ValidateLibraryReferencePlacement(reference, out var placementError))
            {
                return new LibraryReferenceMutationResult(false, placementError);
            }

            var existing = _libraryReferenceStore.Load(FolderPath).ToList();
            var duplicate = existing.FirstOrDefault(item =>
                string.Equals(item.ParentLogicalCode, reference.ParentLogicalCode, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(item.LibraryEntryId, reference.LibraryEntryId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(item.PartId, reference.PartId, StringComparison.OrdinalIgnoreCase)));

            if (duplicate != null)
            {
                duplicate.PartId = reference.PartId;
                duplicate.PartConfigId = reference.PartConfigId;
                duplicate.Revision = reference.Revision;
                duplicate.RevisionPolicy = reference.RevisionPolicy;
                duplicate.LibraryEntryId = reference.LibraryEntryId;
                duplicate.Quantity = reference.Quantity;
                _libraryReferenceStore.Save(FolderPath, existing);
                AnalyzeFolder();
                return new LibraryReferenceMutationResult(true, "Existing Library reference updated in the workspace.");
            }

            _libraryReferenceStore.Upsert(FolderPath, reference);
            AnalyzeFolder();
            return new LibraryReferenceMutationResult(true, "Library reference added to the current workspace.");
        }

        private bool ValidateLibraryReferencePlacement(WorkspaceLibraryReference reference, out string errorMessage)
        {
            errorMessage = null;

            if (reference == null)
            {
                errorMessage = "No Library reference was provided.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(reference.ParentLogicalCode))
            {
                errorMessage = "Choose a target parent in the Product Structure.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(reference.PartId))
            {
                return true;
            }

            var parentPath = FindStructurePath(PdmStructure, reference.ParentLogicalCode);
            if (parentPath == null)
            {
                errorMessage = "Choose a target parent that still exists in the Product Structure.";
                return false;
            }

            if (parentPath.Any(node =>
                node != null &&
                node.IsLibraryReference &&
                !string.IsNullOrWhiteSpace(node.ArasPartId) &&
                string.Equals(node.ArasPartId, reference.PartId, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = "This Library Part would create a self/cycle reference under its own branch.";
                return false;
            }

            return true;
        }

        private static List<PdmStructureNode> FindStructurePath(IEnumerable<PdmStructureNode> roots, string partCode)
        {
            if (roots == null || string.IsNullOrWhiteSpace(partCode))
                return null;

            foreach (var root in roots)
            {
                var path = new List<PdmStructureNode>();
                if (TryFindStructurePath(root, partCode, path))
                    return path;
            }

            return null;
        }

        private static bool TryFindStructurePath(PdmStructureNode node, string partCode, IList<PdmStructureNode> path)
        {
            if (node == null || path == null || string.IsNullOrWhiteSpace(partCode))
                return false;

            path.Add(node);
            if (string.Equals(node.PartCode, partCode, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var child in node.Children)
            {
                if (TryFindStructurePath(child, partCode, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static void CollectParentCandidates(PdmStructureNode node, ICollection<LibraryParentCandidate> candidates)
        {
            if (node == null)
                return;

            candidates.Add(new LibraryParentCandidate
            {
                LogicalCode = node.PartCode,
                DisplayName = node.Name + " (" + node.PartCode + ")"
            });

            foreach (var child in node.Children)
                CollectParentCandidates(child, candidates);
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
                StatusMessage = string.Format(Loc(TranslationKeys.StatusOpenDocFailedBare), ex.Message);
            }
        }

        private async void SaveSelectedNodeToLibraryAsync()
        {
            if (!CanSaveSelectedNodeToLibrary)
                return;

            var selectedNode = SelectedNode;
            if (selectedNode == null)
                return;

            var partLibraryClient = MainViewModel.SharedPartLibraryClient;
            if (partLibraryClient == null)
            {
                StatusMessage = Loc(TranslationKeys.StatusNotConnected);
                return;
            }

            var resolvedPartId = await ResolveSelectedNodePartIdAsync();
            if (string.IsNullOrWhiteSpace(resolvedPartId))
            {
                StatusMessage = Loc(TranslationKeys.LibraryStatusPushPartToArasFirst);
                return;
            }

            var result = await SaveToLibraryWorkflow.ExecuteAsync(
                new PartLibrarySaveSeed
                {
                    PartId = resolvedPartId,
                    PartNumber = selectedNode.PartCode,
                    PartName = selectedNode.Name,
                    SourceProject = SelectedRepository ?? RepositoryCodeForDisplay,
                    SourceCommit = LatestCommitSummary
                },
                partLibraryClient).ConfigureAwait(true);

            if (!result.Submitted)
                return;

            if (result.AddResult?.Success == true)
            {
                AppSessionContext.Current.PendingLibraryFocusLibraryId = result.LibraryId;
                AppSessionContext.Current.PendingLibraryFocusEntryId = result.AddResult.EntryId;
                AppSessionContext.Current.NotifyLibraryDataChanged();

                if (result.AddResult.AlreadyExists)
                {
                    StatusMessage = string.Format(
                        Loc(TranslationKeys.LibraryStatusPartAlreadyInLibrary),
                        result.AddResult.EntryId ?? "-");
                    AppSessionContext.Current.RequestLibraryWorkspace();
                }
                else
                {
                    StatusMessage = string.Format(
                        Loc(TranslationKeys.LibraryStatusPartSavedToLibrary),
                        result.AddResult.EntryId ?? "-");
                }

                return;
            }

            StatusMessage = string.Format(
                Loc(TranslationKeys.LibraryStatusPartSaveFailed),
                result.AddResult?.ErrorMessage ?? result.ErrorMessage ?? Loc(TranslationKeys.UnknownError));
        }

        private async void RemoveSelectedLibraryReferenceAsync()
        {
            if (!HasRemoveLibraryReferenceAction)
                return;

            var selectedNode = SelectedNode;
            if (selectedNode == null || !selectedNode.IsLibraryReference)
                return;

            var confirm = MessageBox.Show(
                "Remove this Library reference from the workspace?",
                "Remove Library Reference",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            if (string.IsNullOrWhiteSpace(selectedNode.ReferenceId))
            {
                StatusMessage = "This Library reference does not have a workspace ID yet.";
                return;
            }

            if (_libraryReferenceStore.Remove(FolderPath, selectedNode.ReferenceId))
            {
                AnalyzeFolder();
                StatusMessage = "Library reference removed from the workspace.";
                return;
            }

            StatusMessage = "Library reference could not be removed from the workspace.";
        }

        private async void OpenInIronCadAsync()
        {
            if (!CanOpenInIronCad)
                return;

            var cadClient = MainViewModel.SharedArasCadClient;
            if (cadClient == null)
            {
                StatusMessage = Loc(TranslationKeys.StatusNotConnected);
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
                    StatusMessage = Loc(TranslationKeys.StatusNoCadOnAras);
                    return;
                }

                var manifest = _workspaceService.LoadManifest(FolderPath);
                var validManifest = manifest != null &&
                    string.Equals(manifest.CadId, _liveCadId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(manifest.ProjectFolder, FolderPath, StringComparison.OrdinalIgnoreCase);

                if (validManifest &&
                    !string.IsNullOrWhiteSpace(manifest.LocalFilePath) &&
                    System.IO.File.Exists(manifest.LocalFilePath) &&
                    !string.IsNullOrWhiteSpace(manifest.LockToken) &&
                    CadLifecyclePolicy.CanCheckout(_liveCadState))
                {
                    var adapter = new IronCadExternalAdapter();
                    await adapter.OpenDocumentAsync(manifest.LocalFilePath, CadOpenMode.Edit, CancellationToken.None);
                    StatusMessage = $"Opened {Path.GetFileName(manifest.LocalFilePath)} (checked out).";
                    return;
                }

                if (validManifest &&
                    !string.IsNullOrWhiteSpace(manifest.LocalFilePath) &&
                    System.IO.File.Exists(manifest.LocalFilePath) &&
                    !string.IsNullOrWhiteSpace(manifest.LockToken) &&
                    !CadLifecyclePolicy.CanCheckout(_liveCadState))
                {
                    var adapter = new IronCadExternalAdapter();
                    await adapter.OpenDocumentAsync(manifest.LocalFilePath, CadOpenMode.ReadOnly, CancellationToken.None);
                    StatusMessage = LifecycleDisplayText.GetReadOnlySessionStaleMessage(Path.GetFileName(manifest.LocalFilePath));
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
                        StatusMessage = string.Format(Loc(TranslationKeys.StatusCadOpenedReadOnly), cadFileName);
                    }
                    else
                    {
                        var fallbackPath = cadPath;
                        if (fallbackPath != null && System.IO.File.Exists(fallbackPath))
                        {
                            var adapter = new IronCadExternalAdapter();
                            await adapter.OpenDocumentAsync(fallbackPath, CadOpenMode.ReadOnly, CancellationToken.None);
                            StatusMessage = string.Format(Loc(TranslationKeys.StatusCadOpenedReadOnlyLocal), cadFileName);
                        }
                        else
                        {
                            StatusMessage = roResult.ErrorMessage ?? Loc(TranslationKeys.StatusCadCannotOpen);
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
                        StatusMessage = string.Format(Loc(TranslationKeys.StatusCadOpenedReadOnlyLifecycle), cadFileName, _liveCadState);
                    }
                    else
                    {
                        StatusMessage = LifecycleDisplayText.GetCheckoutBlockedMessage(_liveCadState);
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
                            StatusMessage = string.Format(Loc(TranslationKeys.StatusCadOpenedReadOnlyLocked), cadFileName);
                        }
                        else
                        {
                            StatusMessage = Loc(TranslationKeys.StatusNoLocalCopyLocked);
                        }
                    }
                    else
                    {
                        StatusMessage = string.Format(Loc(TranslationKeys.StatusCheckoutFailedDetail), checkoutInfo.ErrorMessage);
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
                    Branch = SelectedBranch,
                    LastKnownRevision = checkoutInfo.Cad?.Revision,
                    LastKnownGeneration = checkoutInfo.Cad?.Generation ?? 0
                });

                var editAdapter = new IronCadExternalAdapter();
                await editAdapter.OpenDocumentAsync(checkoutInfo.LocalFilePath, CadOpenMode.Edit, CancellationToken.None);
                StatusMessage = string.Format(Loc(TranslationKeys.StatusCheckedOutPdm), Path.GetFileName(checkoutInfo.LocalFilePath));
                _ = RefreshCadStateAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusOpenIronCadFailed), ex.Message);
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

        private async Task RefreshCadStateAsync(string preferredCadId = null, string preferredPartId = null)
        {
            var cadClient = MainViewModel.SharedArasCadClient;
            _liveCadId = null;
            _liveCadState = null;
            _liveCadRevision = null;
            _liveCadGeneration = 0;
            _liveHasNativeFile = false;
            _livePartId = null;

            // Không xóa push result — node selection không được phá state của PDM.
            // _postPushPartIds = null;

            _isCheckedOutByOther = false;
            SetCadOperationContext(null);

            if (SelectedNode == null || cadClient == null)
            {
                UpdateCadUiState();
                await RefreshRevisionPreconditionsAsync();
                RefreshCanOpenInIronCad();
                return;
            }

            var cadId = !string.IsNullOrWhiteSpace(preferredCadId)
                ? preferredCadId
                : await ResolveCadIdForNodeAsync(SelectedNode, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(cadId))
            {
                UpdateCadUiState();
                await RefreshRevisionPreconditionsAsync();
                RefreshCanOpenInIronCad();
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RefreshCadStateAsync: GetCadOperationContextAsync failed for cadId=" + cadId + ": " + ex.Message);
            }

            var manifest = _workspaceService.LoadManifest(FolderPath);
            _livePartId = !string.IsNullOrWhiteSpace(preferredPartId)
                ? preferredPartId
                : manifest?.PartId;

            UpdateCadUiState();
            await RefreshRevisionPreconditionsAsync();
            RefreshCanOpenInIronCad();
        }

        private void RefreshCanOpenInIronCad()
        {
            OnPropertyChanged(nameof(CanOpenInIronCad));
            OnPropertyChanged(nameof(HasOpenInIronCadAction));
            OnPropertyChanged(nameof(OpenInIronCadModeText));
            OnPropertyChanged(nameof(HasCadRevisionEntryPoint));
            ((RelayCommand)OpenInIronCadCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanStartNewRevision));
            ((RelayCommand)StartNewRevisionCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanSaveSelectedNodeToLibrary));
            ((RelayCommand)SaveSelectedNodeToLibraryCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CadRevisionReadinessText));
            OnPropertyChanged(nameof(HasCadRevisionReadinessInfo));
        }

        private async Task RefreshRevisionPreconditionsAsync()
        {
            var manifest = _workspaceService.LoadManifest(FolderPath);
            _revisionPreconditions = await _revisionService.CheckPreconditionsAsync(
                _liveCadState,
                _liveCadId,
                _livePartId,
                manifest?.LockToken,
                CancellationToken.None);

            CadRevisionReadinessText = GuidanceRevisionService.BuildReadinessText(
                _revisionPreconditions,
                Loc(TranslationKeys.ReadyForRevision),
                Loc(TranslationKeys.RevisionRequiresReleased));
        }

        private bool IsSelectedRootAssemblyNode()
        {
            return SelectedNode != null
                && PdmStructure.Count > 0
                && string.Equals(SelectedNode.PartCode, PdmStructure[0].PartCode, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ResolveSelectedNodePartIdAsync()
        {
            if (SelectedNode == null)
                return null;

            // Library node đã biết sẵn Aras Part ID.
            if (!string.IsNullOrWhiteSpace(SelectedNode.ArasPartId))
                return SelectedNode.ArasPartId;

            // ID vừa nhận được từ kết quả Push.
            if (_postPushPartIds != null &&
                _postPushPartIds.TryGetValue(SelectedNode.PartCode, out var pushedId) &&
                !string.IsNullOrWhiteSpace(pushedId))
            {
                return pushedId;
            }

            // Tìm Part tương ứng trong Push Preview.
            var previewPart = _pushPreview?.Parts?.FirstOrDefault(part =>
                string.Equals(
                    part.LogicalCode,
                    SelectedNode.PartCode,
                    StringComparison.OrdinalIgnoreCase));

            // Part được reuse từ Library.
            if (!string.IsNullOrWhiteSpace(previewPart?.ExistingPartId))
                return previewPart.ExistingPartId;

            // Root Part đã được resolve từ CAD/workspace.
            if (IsSelectedRootAssemblyNode() &&
                !string.IsNullOrWhiteSpace(_livePartId))
            {
                return _livePartId;
            }

            // Fallback: hỏi Aras bằng Part Number.
            var pdmClient = MainViewModel.SharedPdmClient;
            var partNumber = previewPart?.PartNumber ?? SelectedNode.PartCode;

            if (pdmClient != null && !string.IsNullOrWhiteSpace(partNumber))
            {
                return await pdmClient.FindItemIdByNumberAsync(
                    "Part",
                    partNumber,
                    CancellationToken.None);
            }

            return null;
        }

        private bool IsReleasedCad()
        {
            return !string.IsNullOrWhiteSpace(_liveCadState)
                && CadLifecyclePolicy.IsState(_liveCadState, CadLifecyclePolicy.Released);
        }

        private static string Loc(string key) => LocalizationSource.Instance[key];
        private static string Ttl => Loc(TranslationKeys.StartupErrorTitle);

        private static string BuildCadLockStateLabel(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return Loc(TranslationKeys.StateUnknown);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released))
                return Loc(TranslationKeys.CadLockStateReleasedReadOnly);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InReview))
                return Loc(TranslationKeys.CadLockStateInReview);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.InChange))
                return Loc(TranslationKeys.CadLockStateInChange);

            if (CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Superseded)
                || CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Obsolete))
                return Loc(TranslationKeys.CadLockStateSuperseded);

            return Loc(TranslationKeys.StateReadOnly);
        }

        private void UpdateCadUiState()
        {
            if (IsSelectedRootAssemblyNode())
            {
                CadLockStateText = Loc(TranslationKeys.CadLockStateRootAssembly);
                LockedByText = "-";
                CadFileStateText = Loc(TranslationKeys.CadFileStateManagedAssembly);
                CadRevisionText = SelectedNode?.Revision ?? "-";
                CadGenerationText = "-";
                CadLifecycleText = "-";
                CadEditPolicyText = CadNodeHelper.GetRootAssemblyCadHint();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = false;
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
            var stateEditable = !string.IsNullOrWhiteSpace(_liveCadState) &&
                CadLifecyclePolicy.CanCheckout(_liveCadState);

            if (string.IsNullOrWhiteSpace(_liveCadId))
            {
                CadLockStateText = Loc(TranslationKeys.CadLockStateNotPushed);
                LockedByText = "-";
                CadFileStateText = Loc(TranslationKeys.CadLockStateArasUnavailable);
                CadRevisionText = SelectedNode?.Revision ?? "-";
                CadGenerationText = "-";
                CadLifecycleText = "-";
                CadEditPolicyText = Loc(TranslationKeys.CadEditPolicyPushFirst);
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = false;
                return;
            }

            if (validManifest && !string.IsNullOrWhiteSpace(manifest.LockToken) &&
                !string.IsNullOrWhiteSpace(_liveCadState) &&
                !stateEditable)
            {
                CadLockStateText = LifecycleDisplayText.GetStaleSessionLabel();
                LockedByText = manifest.LockedBy ?? "-";
                CadFileStateText = LifecycleDisplayText.GetStaleSessionMessage(_liveCadState);
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = string.Equals(manifest.LockedBy, currentUser, StringComparison.OrdinalIgnoreCase);
                return;
            }

            if (validManifest && !string.IsNullOrWhiteSpace(manifest.LockToken) &&
                !string.Equals(manifest.LockedBy, currentUser, StringComparison.OrdinalIgnoreCase))
            {
                CadLockStateText = LifecycleDisplayText.GetStaleSessionLabel();
                LockedByText = manifest.LockedBy ?? "-";
                CadFileStateText = LifecycleDisplayText.GetDifferentUserSessionMessage(manifest.LockedBy);
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = false;
                return;
            }

            if (myLockInManifest && myLocalFileExists && stateEditable)
            {
                CadLockStateText = Loc(TranslationKeys.CadLockStateCheckedOutByMe);
                LockedByText = manifest.LockedBy;
                CadFileStateText = Loc(TranslationKeys.CadFileStateLocalWorkingCopy);
                ApplyCadRevisionState();
                IsCheckedOutByMe = true;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = true;
                CanCancelCheckout = true;
                return;
            }

            if (myLockInManifest && myLocalFileExists && !stateEditable)
            {
                CadLockStateText = Loc(TranslationKeys.CadLockStateStaleSession);
                LockedByText = manifest.LockedBy;
                CadFileStateText = LifecycleDisplayText.GetStaleSessionMessage(_liveCadState);
                ApplyCadRevisionState();
                IsCheckedOutByMe = true;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = true;
                return;
            }

            if (myLockInManifest && !myLocalFileExists && stateEditable)
            {
                CadLockStateText = Loc(TranslationKeys.CadLockStateMissingLocalFile);
                LockedByText = manifest.LockedBy;
                CadFileStateText = Loc(TranslationKeys.CadFileStateLocalFileNotFound);
                ApplyCadRevisionState();
                IsCheckedOutByMe = true;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = true;
                return;
            }

            if (myLockInManifest && !myLocalFileExists && !stateEditable)
            {
                CadLockStateText = LifecycleDisplayText.GetCheckedOutByMeFileMissingStaleLabel();
                LockedByText = manifest.LockedBy;
                CadFileStateText = string.Concat(Loc(TranslationKeys.CadFileStateLocalFileNotFound), " ", LifecycleDisplayText.GetStaleSessionMessage(_liveCadState));
                ApplyCadRevisionState();
                IsCheckedOutByMe = true;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = true;
                return;
            }

            if (IsCheckedOutByOther)
            {
                CadLockStateText = Loc(TranslationKeys.CadLockStateCheckedOutByOther);
                LockedByText = _lockedByText ?? "-";
                CadFileStateText = Loc(TranslationKeys.CadFileStateLockedByOther);
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = true;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_liveCadState) &&
                !stateEditable)
            {
                CadLockStateText = BuildCadLockStateLabel(_liveCadState);
                LockedByText = "-";
                CadFileStateText = LifecycleDisplayText.GetCheckoutBlockedMessage(_liveCadState);
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = false;
                CanCheckIn = false;
                CanCancelCheckout = false;
                return;
            }

            if (!_liveHasNativeFile)
            {
                CadLockStateText = Loc(TranslationKeys.CadLockStateMissingNative);
                LockedByText = "-";
                CadFileStateText = Loc(TranslationKeys.CadFileStateNoNativeOnAras);
                ApplyCadRevisionState();
                IsCheckedOutByMe = false;
                IsCheckedOutByOther = false;
                IsAvailable = true;
                CanCheckIn = false;
                CanCancelCheckout = false;
                return;
            }

            CadLockStateText = Loc(TranslationKeys.AvailableShort);
            LockedByText = "-";
            CadFileStateText = Loc(TranslationKeys.CadFileStateReadyForCheckout);
            ApplyCadRevisionState();
            IsCheckedOutByMe = false;
            IsCheckedOutByOther = false;
            IsAvailable = true;
            CanCheckIn = false;
            CanCancelCheckout = false;
        }

        private void ApplyCadRevisionState()
        {
            CadRevisionText = string.IsNullOrWhiteSpace(_liveCadRevision) ? "-" : _liveCadRevision;
            CadGenerationText = _liveCadGeneration > 0
                ? _liveCadGeneration.ToString(CultureInfo.InvariantCulture)
                : "-";
            CadLifecycleText = string.IsNullOrWhiteSpace(_liveCadState) ? "-" : _liveCadState;
            CadEditPolicyText = BuildCadEditPolicyText(_liveCadState);
            CadDriftText = BuildCadDriftText();
        }

        private string BuildCadDriftText()
        {
            var manifest = _workspaceService.LoadManifest(FolderPath);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.LastKnownRevision))
                return null;

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(_liveCadRevision) &&
                !string.Equals(manifest.LastKnownRevision, _liveCadRevision, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"Revision changed from {manifest.LastKnownRevision} to {_liveCadRevision}");
            }

            if (manifest.LastKnownGeneration > 0 && _liveCadGeneration > 0 &&
                manifest.LastKnownGeneration != _liveCadGeneration)
            {
                parts.Add($"Generation changed from {manifest.LastKnownGeneration} to {_liveCadGeneration}");
            }

            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }

        private static string BuildCadEditPolicyText(string state)
        {
            return LifecycleDisplayText.GetActionGuidance(state);
        }

        private void SetCadOperationContext(CadOperationContext context)
        {
            _cadOperationContext = context;
            OnPropertyChanged(nameof(WorkflowStatusText));
            OnPropertyChanged(nameof(HasAnyCadBusinessAction));
            OnPropertyChanged(nameof(HasStartDetailedDesignBusinessAction));
            OnPropertyChanged(nameof(HasSubmitForReviewBusinessAction));
            OnPropertyChanged(nameof(HasApproveBusinessAction));
            OnPropertyChanged(nameof(HasRequestReworkBusinessAction));
            _startDetailedDesignCommand?.RaiseCanExecuteChanged();
            _submitForReviewCommand?.RaiseCanExecuteChanged();
            _approveCadCommand?.RaiseCanExecuteChanged();
            _requestReworkCommand?.RaiseCanExecuteChanged();
        }

        private bool HasCadAction(CadBusinessActionKind kind)
        {
            return !string.IsNullOrWhiteSpace(_liveCadState)
                && CadLifecyclePolicy.ShouldShowBusinessAction(kind, _liveCadState);
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
                StatusMessage = Loc(TranslationKeys.StatusNoLiveCad);
                return;
            }

            var confirmMessage = kind switch
            {
                CadBusinessActionKind.StartDetailedDesign => Loc(TranslationKeys.ConfirmStartDetailedDesign),
                CadBusinessActionKind.SubmitForReview => Loc(TranslationKeys.ConfirmSubmitForReview),
                CadBusinessActionKind.Approve => Loc(TranslationKeys.ConfirmApprove),
                CadBusinessActionKind.RequestRework => Loc(TranslationKeys.ConfirmRequestRework),
                _ => string.Format(Loc(TranslationKeys.ConfirmExecuteAction), kind)
            };

            var confirmResult = System.Windows.MessageBox.Show(
                confirmMessage,
                Loc(TranslationKeys.WorkflowActionTitle),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (confirmResult != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsOpeningInIronCad = true;
                StatusMessage = string.Format(Loc(TranslationKeys.StatusExecutingAction), kind);

                var freshContext = await cadClient.GetCadOperationContextAsync(_liveCadId, CancellationToken.None);
                if (freshContext == null)
                {
                    throw new InvalidOperationException("Cannot load fresh CAD workflow context.");
                }

                SetCadOperationContext(freshContext);

                var action = freshContext.AvailableActions?.FirstOrDefault(a => a.Kind == kind && a.IsAvailable)
                    ?? new CadBusinessAction(kind, kind.ToString(), true, null, false, null, null);

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
                StatusMessage = string.Format(Loc(TranslationKeys.StatusActionCompleted), kind);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusActionFailed), kind) + " " + ex.Message;
                System.Windows.MessageBox.Show(
                    string.Format(Loc(TranslationKeys.MsgWorkflowActionFailed), kind, ex.Message),
                    Loc(TranslationKeys.WorkflowActionTitle),
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
                StatusMessage = Loc(TranslationKeys.StatusNotConnected);
                return;
            }

            var manifest = _workspaceService.LoadManifest(FolderPath);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.LockToken) || string.IsNullOrWhiteSpace(manifest.CadId))
            {
                StatusMessage = Loc(TranslationKeys.ErrorNoCheckoutSession);
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
                    StatusMessage = Loc(TranslationKeys.ErrorLocalFileNotFound);
                    return;
                }

                var result = await _checkoutService.UploadAndCheckinAsync(
                    manifest.CadId, manifest.LockToken, filePath, null, CancellationToken.None);

                if (result.Success)
                {
                    _workspaceService.ClearManifest(FolderPath);
                    await RefreshCadStateAsync();
                    StatusMessage = Loc(TranslationKeys.StatusCheckinCompletedPdm);
                }
                else
                {
                    StatusMessage = string.Format(Loc(TranslationKeys.StatusCheckinFailedDetail), result.ErrorMessage ?? Loc(TranslationKeys.UnknownError));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusCheckinFailedDetail), ex.Message);
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
                StatusMessage = Loc(TranslationKeys.StatusNotConnected);
                return;
            }

            var manifest = _workspaceService.LoadManifest(FolderPath);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.CadId))
            {
                StatusMessage = Loc(TranslationKeys.StatusNoCheckoutToCancel);
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
                    StatusMessage = Loc(TranslationKeys.StatusCheckoutCancelled);
                }
                else
                {
                    _workspaceService.ClearManifest(FolderPath);
                    await RefreshCadStateAsync();
                    StatusMessage = Loc(TranslationKeys.StatusCheckoutCancelledServer);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusCancelFailedDetail), ex.Message);
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
                AnalysisSummary = Loc(TranslationKeys.PdmNoFolderHint);
                StatusMessage = Loc(TranslationKeys.StatusFolderInvalid);
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

            analyzeResult = AppendLibraryReferenceNodes(analyzeResult);

            var builder = new PdmPushPreviewBuilder();
            _pushPreview = builder.Build(analyzeResult, SelectedBranch, CommitMessage);

            var staleWarnings = BuildStalePushWarnings();
            var cycleWarnings = ValidateStructureCycles();
            var allWarnings = new List<PreviewWarning>(_pushPreview.Warnings);
            allWarnings.AddRange(staleWarnings);
            allWarnings.AddRange(cycleWarnings);
            var blockingCount = allWarnings.Count(w => w.BlocksPush);
            var canPush = _pushPreview.Readiness.CanPush && blockingCount == 0;
            _pushPreview = new PushPreview
            {
                RepositoryCode = _pushPreview.RepositoryCode,
                ProjectName = _pushPreview.ProjectName,
                TargetBranch = _pushPreview.TargetBranch,
                CommitMessage = _pushPreview.CommitMessage,
                Parts = _pushPreview.Parts,
                Cads = _pushPreview.Cads,
                Documents = _pushPreview.Documents,
                IgnoredFiles = _pushPreview.IgnoredFiles,
                Warnings = allWarnings,
                Readiness = new PushReadiness
                {
                    CanPush = canPush,
                    HasBlockingIssues = blockingCount > 0,
                    BlockingIssueCount = blockingCount,
                    Summary = blockingCount > 0
                        ? "Workspace session has blocking issues. Resolve before push."
                        : "Workspace session has warnings. Review before push."
                }
            };

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

        private List<PreviewWarning> BuildStalePushWarnings()
        {
            var manifest = _workspaceService.LoadManifest(FolderPath);
            var warnings = new List<PreviewWarning>();

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.LockToken))
                return warnings;

            var manifestMatchesLive = !string.IsNullOrWhiteSpace(_liveCadId) &&
                string.Equals(manifest.CadId, _liveCadId, StringComparison.OrdinalIgnoreCase);

            var currentUser = MainViewModel.SharedUserName ?? string.Empty;
            var mySession = string.Equals(manifest.LockedBy, currentUser, StringComparison.OrdinalIgnoreCase);

            if (!mySession)
            {
                warnings.Add(new PreviewWarning
                {
                    Source = "WorkspaceSession",
                    Message = "Local checkout session belongs to '" + manifest.LockedBy + "'. Push is blocked until the correct user resolves the session.",
                    BlocksPush = true
                });
                return warnings;
            }

            var myLocalFileExists = !string.IsNullOrWhiteSpace(manifest.LocalFilePath) &&
                System.IO.File.Exists(manifest.LocalFilePath);

            if (!myLocalFileExists)
            {
                warnings.Add(new PreviewWarning
                {
                    Source = "WorkspaceSession",
                    Message = "Local file missing for active checkout session. Cancel checkout to release the lock before push, or restore the file from the workspace.",
                    BlocksPush = true
                });
            }

            if (manifestMatchesLive &&
                !string.IsNullOrWhiteSpace(_liveCadState) &&
                !CadLifecyclePolicy.CanCheckout(_liveCadState))
            {
                warnings.Add(new PreviewWarning
                {
                    Source = "WorkspaceSession",
                    Message = LifecycleDisplayText.GetPushSessionStaleMessage(_liveCadState),
                    BlocksPush = true
                });
            }

            if (!manifestMatchesLive && !string.IsNullOrWhiteSpace(manifest.CadId))
            {
                warnings.Add(new PreviewWarning
                {
                    Source = "WorkspaceSession",
                    Message = "Manifest CAD does not match the selected live CAD. The checkout session belongs to a different CAD record.",
                    BlocksPush = false
                });
            }

            if (!string.IsNullOrWhiteSpace(manifest.LastKnownRevision) &&
                !string.IsNullOrWhiteSpace(_liveCadRevision) &&
                !string.Equals(manifest.LastKnownRevision, _liveCadRevision, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new PreviewWarning
                {
                    Source = "RevisionDrift",
                    Message = LifecycleDisplayText.GetRevisionDriftMessage(manifest.LastKnownRevision, _liveCadRevision),
                    BlocksPush = false
                });
            }

            if (manifest.LastKnownGeneration > 0 && _liveCadGeneration > 0 &&
                manifest.LastKnownGeneration != _liveCadGeneration)
            {
                warnings.Add(new PreviewWarning
                {
                    Source = "GenerationDrift",
                    Message = LifecycleDisplayText.GetGenerationDriftMessage(manifest.LastKnownGeneration, _liveCadGeneration),
                    BlocksPush = false
                });
            }

            return warnings;
        }

        private IReadOnlyList<PreviewWarning> ValidateStructureCycles()
        {
            var warnings = new List<PreviewWarning>();
            var parts = _pushPreview?.Parts;
            if (parts == null || parts.Count == 0)
                return warnings;

            var parentByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var codeToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part.LogicalCode))
                {
                    codeToName[part.LogicalCode] = part.PartNumber ?? part.Name ?? part.LogicalCode;
                    if (!string.IsNullOrWhiteSpace(part.ParentLogicalCode))
                        parentByCode[part.LogicalCode] = part.ParentLogicalCode;
                }
            }

            foreach (var part in parts)
            {
                var code = part.LogicalCode;
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                if (string.Equals(code, part.ParentLogicalCode, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(new PreviewWarning
                    {
                        Source = "CycleDetection",
                        Message = $"Self-reference detected: Part '{codeToName[code]}' has itself as its own parent.",
                        BlocksPush = true
                    });
                }
            }

            var duplicateKeyCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var duplicateKeyParts = new Dictionary<string, (string Parent, string Child)>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in parts)
            {
                if (string.IsNullOrWhiteSpace(p.LogicalCode) || string.IsNullOrWhiteSpace(p.ParentLogicalCode))
                    continue;
                var dupKey = p.ParentLogicalCode + "||" + p.LogicalCode;
                if (!duplicateKeyCount.ContainsKey(dupKey))
                {
                    duplicateKeyCount[dupKey] = 0;
                    duplicateKeyParts[dupKey] = (p.ParentLogicalCode, p.LogicalCode);
                }
                duplicateKeyCount[dupKey]++;
            }
            foreach (var kvp in duplicateKeyCount)
            {
                if (kvp.Value <= 1) continue;
                var pair = duplicateKeyParts[kvp.Key];
                codeToName.TryGetValue(pair.Child, out var childName);
                codeToName.TryGetValue(pair.Parent, out var parentName);
                warnings.Add(new PreviewWarning
                {
                    Source = "CycleDetection",
                    Message = $"Duplicate part '{childName ?? pair.Child}' appears {kvp.Value} times under parent '{parentName ?? pair.Parent}'.",
                    BlocksPush = true
                });
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in parts)
            {
                var code = part.LogicalCode;
                if (string.IsNullOrWhiteSpace(code) || visited.Contains(code))
                    continue;

                var path = new List<string>();
                var current = code;
                while (!string.IsNullOrWhiteSpace(current) && parentByCode.ContainsKey(current))
                {
                    if (inStack.Contains(current))
                    {
                        var cycleStart = path.IndexOf(current);
                        var cycleNames = cycleStart >= 0
                            ? string.Join(" → ", path.Skip(cycleStart).Select(c => codeToName.TryGetValue(c, out var n) ? n : c))
                            : (codeToName.TryGetValue(current, out var n) ? n : current);
                        warnings.Add(new PreviewWarning
                        {
                            Source = "CycleDetection",
                            Message = $"Circular BOM reference detected: {cycleNames} creates a cycle. Push is blocked until the cycle is removed.",
                            BlocksPush = true
                        });
                        break;
                    }

                    if (visited.Contains(current))
                        break;

                    inStack.Add(current);
                    path.Add(current);

                    if (!parentByCode.TryGetValue(current, out var next))
                        break;
                    current = next;
                }

                foreach (var n in path)
                {
                    visited.Add(n);
                    inStack.Remove(n);
                }
            }

            return warnings;
        }

        private void LoadBranchesForFolder()
        {
            Branches.Clear();
            _workspaceService.EnsureMainBranch(FolderPath);
            var registry = _workspaceService.LoadBranchRegistry(FolderPath);
            foreach (var b in registry.Branches)
                Branches.Add(b.Name);
            if (Branches.Count > 0 && SelectedBranch == null)
                SelectedBranch = Branches[0];
            else if (Branches.Count > 0 && !Branches.Contains(SelectedBranch))
                SelectedBranch = Branches[0];
            OnPropertyChanged(nameof(IsMainBranch));
            OnPropertyChanged(nameof(BranchPushAllowed));
            OnPropertyChanged(nameof(BranchStatusText));
        }

        private void EnsureLocalBranchExists(string projectFolder, string branchName)
        {
            if (string.IsNullOrWhiteSpace(projectFolder) || string.IsNullOrWhiteSpace(branchName))
                return;

            var registry = _workspaceService.LoadBranchRegistry(projectFolder);
            if (registry.Branches.Any(b => string.Equals(b.Name, branchName, StringComparison.OrdinalIgnoreCase)))
                return;

            registry.Branches.Add(new WorkspaceBranch
            {
                Name = branchName,
                CreatedAt = DateTime.UtcNow
            });

            _workspaceService.SaveBranchRegistry(projectFolder, registry);
        }

        // TODO(PERF-CONTENT-HASH): Replace with SHA256-based PdmContentHasher
        // when Phase 1 workspace index is introduced. Current signature
        // compares file identity only (sorted paths + structure keys), not
        // file content.
        private string ComputeSnapshotSignature()
        {
            if (_latestAnalysis == null)
                return null;
            var parts = new System.Collections.Generic.List<string>();

            var tracked = _latestAnalysis.TrackedFiles
                .Select(f => f.RelativePath ?? f.FileName ?? "")
                .Where(p => p.Length > 0)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            parts.Add("TF:" + string.Join(",", tracked));

            var docs = _latestAnalysis.DocumentFiles
                .Select(f => f.RelativePath ?? f.FileName ?? "")
                .Where(p => p.Length > 0)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            parts.Add("DOC:" + string.Join(",", docs));

            var primary = _latestAnalysis.PrimaryAssembly;
            parts.Add("PA:" + (primary?.RelativePath ?? primary?.FileName ?? ""));

            parts.Add("PC:" + (_latestAnalysis.ProjectCode ?? ""));

            var nodeKeys = new System.Collections.Generic.List<string>();
            if (_latestBusinessStructure?.RootNodes != null)
            {
                foreach (var root in _latestBusinessStructure.RootNodes)
                    FlattenNodeKeys(root, nodeKeys);
            }
            nodeKeys.Sort(StringComparer.OrdinalIgnoreCase);
            parts.Add("SN:" + string.Join(",", nodeKeys));

            var libraryKeys = _workspaceLibraryReferences
                .OrderBy(reference => reference.ReferenceId, StringComparer.OrdinalIgnoreCase)
                .Select(reference =>
                    (reference.ReferenceId ?? string.Empty) + ":" +
                    (reference.LibraryEntryId ?? string.Empty) + ":" +
                    (reference.PartId ?? string.Empty) + ":" +
                    (reference.ParentLogicalCode ?? string.Empty) + ":" +
                    reference.Quantity + ":" +
                    (reference.RevisionPolicy ?? string.Empty) + ":" +
                    (reference.Revision ?? string.Empty));
            parts.Add("LIB:" + string.Join(",", libraryKeys));

            return string.Join("|", parts);
        }

        private static void FlattenNodeKeys(PdmBusinessNode node, System.Collections.Generic.List<string> keys)
        {
            keys.Add(node.Code ?? node.Name ?? "");
            foreach (var child in node.Children)
                FlattenNodeKeys(child, keys);
        }

        private WorkspaceCommit GetLatestCommitForBranch()
        {
            var history = _workspaceService.LoadCommitHistory(FolderPath);
            if (history?.Commits == null || history.Commits.Count == 0)
                return null;
            var branch = SelectedBranch ?? "main";
            return history.Commits
                .Where(c => string.Equals(c.Branch, branch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.Timestamp)
                .FirstOrDefault();
        }

        private async void ExecuteNewBranchAsync()
        {
            var dialog = new System.Windows.Window
            {
                Title = "New Branch",
                Width = 400,
                Height = 200,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Content = new System.Windows.Controls.StackPanel
                {
                    Margin = new System.Windows.Thickness(20),
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text = "Enter a name for the new branch:",
                            Margin = new System.Windows.Thickness(0, 0, 0, 12),
                            FontSize = 14
                        },
                        new System.Windows.Controls.TextBox
                        {
                            Name = "branchNameBox",
                            FontSize = 14,
                            Height = 32
                        },
                        new System.Windows.Controls.Button
                        {
                            Content = Loc(TranslationKeys.ButtonCreateBranch),
                            Height = 36,
                            Margin = new System.Windows.Thickness(0, 12, 0, 0),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                            Width = 140,
                            IsDefault = true
                        }
                    }
                }
            };

            var textBox = (System.Windows.Controls.TextBox)((System.Windows.Controls.StackPanel)dialog.Content).Children[1];
            var createButton = (System.Windows.Controls.Button)((System.Windows.Controls.StackPanel)dialog.Content).Children[2];
            createButton.Click += (s, e) =>
            {
                var name = textBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show(Loc(TranslationKeys.BranchNameCannotBeEmpty), Loc(TranslationKeys.NewBranchTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (Branches.Any(b => string.Equals(b, name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(Loc(TranslationKeys.BranchNameExists), Loc(TranslationKeys.NewBranchTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var registry = _workspaceService.LoadBranchRegistry(FolderPath);
                registry.Branches.Add(new WorkspaceBranch
                {
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                });
                _workspaceService.SaveBranchRegistry(FolderPath, registry);
                Branches.Add(name);
                SelectedBranch = name;
                dialog.Close();
            };

            dialog.ShowDialog();
        }

        private void LoadCommitHistoryForFolder()
        {
            WorkspaceCommits.Clear();
            var history = _workspaceService.LoadCommitHistory(FolderPath);
            if (history?.Commits != null)
            {
                var branch = SelectedBranch ?? "main";
                foreach (var c in history.Commits.Where(c => string.Equals(c.Branch, branch, StringComparison.OrdinalIgnoreCase)))
                    WorkspaceCommits.Add(c);
            }
            if (WorkspaceCommits.Count > 0)
                SelectedCommit = WorkspaceCommits[WorkspaceCommits.Count - 1];
            else
                SelectedCommit = null;
        }

        private Task ExecuteCommitAsync()
        {
            if (!CanCommit)
                return Task.CompletedTask;

            // TODO(PERF-COMMIT-FILES): Add PdmCommitFileEntry collection when
            // per-file model is introduced. Currently stores only aggregate
            // counts (CadFileCount, DocumentFileCount, StructureNodeCount).
            var commit = new WorkspaceCommit
            {
                CommitId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.UtcNow,
                Branch = SelectedBranch ?? "main",
                Message = CommitMessage ?? "Workspace commit",
                ProjectFolder = FolderPath,
                RepositoryCode = _latestAnalysis?.ProjectCode,
                StructureNodeCount = CountBusinessNodes(_latestBusinessStructure),
                CadFileCount = (_latestAnalysis?.AssemblyFiles?.Count ?? 0) + (_latestAnalysis?.DetailFiles?.Count ?? 0),
                DocumentFileCount = _latestAnalysis?.DocumentFiles?.Count ?? 0,
                LibraryReferenceCount = _workspaceLibraryReferences.Count,
                SnapshotSignature = ComputeSnapshotSignature()
            };

            var history = _workspaceService.LoadCommitHistory(FolderPath);
            history.Commits.Add(commit);
            _workspaceService.SaveCommitHistory(FolderPath, history);

            WorkspaceCommits.Add(commit);
            SelectedCommit = commit;

            CommitMessage = null;
            OnPropertyChanged(nameof(LatestCommitSummary));
            OnPropertyChanged(nameof(HasUncommittedChanges));
            OnPropertyChanged(nameof(CanCommit));
            (CommitCommand as RelayCommand)?.RaiseCanExecuteChanged();

            StatusMessage = string.Format(Loc(TranslationKeys.StatusCommitSavedOn), commit.Branch);
            return Task.CompletedTask;
        }

        private static int CountBusinessNodes(PdmBusinessStructureAnalysis businessStructure)
        {
            if (businessStructure?.RootNodes == null)
                return 0;

            return businessStructure.RootNodes.Sum(CountBusinessNodeRecursive);
        }

        private static int CountBusinessNodeRecursive(PdmBusinessNode node)
        {
            if (node == null)
                return 0;

            return 1 + node.Children.Sum(CountBusinessNodeRecursive);
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
                    var exists = string.Equals(part.SourceKind, LibrarySourceKind.LibraryReference.ToString(), StringComparison.OrdinalIgnoreCase)
                        || (existence.PartsByNumber.TryGetValue(part.PartNumber, out var e) && e);
                    var action = string.Equals(part.SourceKind, LibrarySourceKind.LibraryReference.ToString(), StringComparison.OrdinalIgnoreCase)
                        ? "Reuse from Library"
                        : (exists ? "Reuse" : "Create");
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
                        Action = action,
                        ExistingPartId = part.ExistingPartId,
                        ExistingPartConfigId = part.ExistingPartConfigId,
                        ExistingPartRevision = part.ExistingPartRevision,
                        SourceKind = part.SourceKind,
                        LibraryEntryId = part.LibraryEntryId,
                        RevisionPolicy = part.RevisionPolicy,
                        IsExternalReference = part.IsExternalReference
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
                StatusMessage = string.Format(Loc(TranslationKeys.StatusComparePreviewFailed), ex.Message);
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

        public void RefreshLocalization()
        {
            OnPropertyChanged(nameof(AnalysisSummary));
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(WorkflowStatusText));
            OnPropertyChanged(nameof(CadLockStateText));
            OnPropertyChanged(nameof(LockedByText));
            OnPropertyChanged(nameof(CadFileStateText));
            OnPropertyChanged(nameof(CadRevisionText));
            OnPropertyChanged(nameof(CadGenerationText));
            OnPropertyChanged(nameof(CadLifecycleText));
            OnPropertyChanged(nameof(CadEditPolicyText));
            OnPropertyChanged(nameof(CadDriftText));
            OnPropertyChanged(nameof(BranchStatusText));
            OnPropertyChanged(nameof(LatestCommitSummary));
            OnPropertyChanged(nameof(ConnectionDisplayName));
            OnPropertyChanged(nameof(CanPush));
            OnPropertyChanged(nameof(CanCommit));
            OnPropertyChanged(nameof(IsMainBranch));
            OnPropertyChanged(nameof(BranchPushAllowed));
            OnPropertyChanged(nameof(HasUncommittedChanges));
            OnPropertyChanged(nameof(SelectedNode));
            OnPropertyChanged(nameof(HasSaveToLibraryAction));
            OnPropertyChanged(nameof(CanSaveSelectedNodeToLibrary));
            (SaveSelectedNodeToLibraryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasRemoveLibraryReferenceAction));
            (RemoveSelectedLibraryReferenceCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
            string sourceDocument = null,
            string sourceKind = null,
            string libraryEntryId = null,
            string arasPartId = null,
            string arasConfigId = null,
            string revisionPolicy = null,
            bool isLibraryReference = false,
            string referenceId = null)
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
            SourceKind = sourceKind ?? LibrarySourceKind.Generated.ToString();
            LibraryEntryId = libraryEntryId;
            ArasPartId = arasPartId;
            ArasConfigId = arasConfigId;
            RevisionPolicy = revisionPolicy;
            IsLibraryReference = isLibraryReference;
            ReferenceId = referenceId;
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
        public string SourceKind { get; }
        public string LibraryEntryId { get; }
        public string ArasPartId { get; }
        public string ArasConfigId { get; }
        public string RevisionPolicy { get; }
        public bool IsLibraryReference { get; }
        public string ReferenceId { get; }
        public ObservableCollection<PdmStructureNode> Children { get; }
    }

    public sealed class LibraryReferenceMutationResult
    {
        public LibraryReferenceMutationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        public string Message { get; }
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
