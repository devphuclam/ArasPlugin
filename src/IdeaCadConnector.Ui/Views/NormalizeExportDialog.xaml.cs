using System;
using System.IO;
using System.Linq;
using System.Windows;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.Ui.Views
{
    public partial class NormalizeExportDialog : Window
    {
        private readonly PdmNormalizationPlan _plan;

        public NormalizeExportDialog(PdmNormalizationPlan plan, string defaultOutputFolder)
        {
            InitializeComponent();
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            ProjectCodeBox.Text = plan.ProjectCode;
            RevisionBox.Text = string.IsNullOrWhiteSpace(plan.Revision) ? "A" : plan.Revision;
            OutputFolderBox.Text = defaultOutputFolder ?? string.Empty;
            ItemsGrid.ItemsSource = plan.Items.ToList();
            SummaryText.Text = string.Format("Assemblies: {0} | Parts: {1} | Warnings: {2}",
                plan.Assemblies.Count, plan.Parts.Count, plan.Warnings.Count);
        }

        public string ProjectCode { get; private set; }
        public string Revision { get; private set; }
        public string OutputFolder { get; private set; }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProjectCode = PdmNameNormalizer.NormalizeProjectCode(ProjectCodeBox.Text);
                Revision = string.IsNullOrWhiteSpace(RevisionBox.Text) ? "A" : RevisionBox.Text.Trim().ToUpperInvariant();
                OutputFolder = OutputFolderBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(OutputFolder)) throw new InvalidOperationException("Output folder is required.");
                foreach (var item in _plan.Items)
                    item.CanonicalFileName = PdmNameNormalizer.CreateCanonicalFileName(
                        ProjectCode, item.ItemType, item.ItemCode, item.DisplayName);
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
}
