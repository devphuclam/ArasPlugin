using System;
using System.Linq;
using System.Windows;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.Ui.Views
{
    public partial class NormalizeExportDialog : Window
    {
        private readonly PdmNormalizationPlan _plan;
        private readonly System.Collections.Generic.List<NormalizeExportEditRow> _rows;

        public NormalizeExportDialog(PdmNormalizationPlan plan, string defaultOutputFolder)
        {
            InitializeComponent();
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            ProjectCodeBox.Text = plan.ProjectCode;
            RevisionBox.Text = string.IsNullOrWhiteSpace(plan.Revision) ? "A" : plan.Revision;
            OutputFolderBox.Text = defaultOutputFolder ?? string.Empty;
            _rows = plan.Items.Select(i => new NormalizeExportEditRow
            {
                SourceNode = i.SourceNode,
                EditKey = i.EditKey,
                OccurrencePath = i.OccurrencePath,
                NodeId = i.NodeId,
                CurrentSceneName = i.SourceNode == null ? string.Empty : i.SourceNode.Name,
                ItemCode = i.ItemCode,
                DisplayName = i.DisplayName,
                OriginalItemCode = i.ItemCode,
                OriginalDisplayName = i.DisplayName,
                SourceWasGeneric = i.SourceWasGeneric,
                CanonicalFileName = i.CanonicalFileName,
                Depth = i.Depth,
                ItemType = i.ItemType
            }).ToList();
            ItemsGrid.ItemsSource = _rows;
            SummaryText.Text = string.Format("Assemblies: {0} | Parts: {1} | Warnings: {2}",
                plan.Assemblies.Count, plan.Parts.Count, plan.Warnings.Count);
        }

        public NormalizeExportDialogResult Result { get; private set; }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var projectCode = PdmNameNormalizer.NormalizeProjectCode(ProjectCodeBox.Text);
                var revision = string.IsNullOrWhiteSpace(RevisionBox.Text) ? "A" : RevisionBox.Text.Trim().ToUpperInvariant();
                var outputFolder = OutputFolderBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(outputFolder)) throw new InvalidOperationException("Output folder is required.");
                if (_rows.Any(r => string.IsNullOrWhiteSpace(r.ItemCode) || string.IsNullOrWhiteSpace(r.DisplayName)))
                    throw new InvalidOperationException("Item code and display name are required.");
                Result = new NormalizeExportDialogResult
                {
                    ProjectCode = projectCode,
                    Revision = revision,
                    OutputFolder = outputFolder,
                    Edits = _rows.Select(r => new NormalizeExportEdit
                    {
                        SourceNode = r.SourceNode,
                        EditKey = r.EditKey,
                        OccurrencePath = r.OccurrencePath,
                        NodeId = r.NodeId,
                        ItemCode = PdmNameNormalizer.NormalizeCode(r.ItemCode),
                        DisplayName = PdmNameNormalizer.NormalizeDisplayName(r.DisplayName),
                        GenericNameConfirmed = !r.SourceWasGeneric ||
                            r.GenericNameConfirmed ||
                            !string.Equals(PdmNameNormalizer.NormalizeCode(r.ItemCode), r.OriginalItemCode, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(PdmNameNormalizer.NormalizeDisplayName(r.DisplayName), r.OriginalDisplayName, StringComparison.OrdinalIgnoreCase)
                    }).ToArray()
                };
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Chuẩn hóa & Xuất PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    internal sealed class NormalizeExportEditRow
    {
        public PdmSourceNode SourceNode { get; set; }
        public string EditKey { get; set; }
        public string OccurrencePath { get; set; }
        public string NodeId { get; set; }
        public string CurrentSceneName { get; set; }
        public string ItemCode { get; set; }
        public string DisplayName { get; set; }
        public bool GenericNameConfirmed { get; set; }
        public string OriginalItemCode { get; set; }
        public string OriginalDisplayName { get; set; }
        public bool SourceWasGeneric { get; set; }
        public string CanonicalFileName { get; set; }
        public int Depth { get; set; }
        public string ItemType { get; set; }
    }
}
