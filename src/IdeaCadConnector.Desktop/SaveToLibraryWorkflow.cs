using System;
using System.Threading.Tasks;
using System.Windows;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop
{
    internal static class SaveToLibraryWorkflow
    {
        public static async Task<SaveToLibraryWorkflowResult> ExecuteAsync(
            PartLibrarySaveSeed seed,
            IPartLibraryClient client)
        {
            if (seed == null)
                throw new ArgumentNullException(nameof(seed));

            if (client == null)
            {
                return new SaveToLibraryWorkflowResult
                {
                    Submitted = false,
                    ErrorMessage = LocalizationSource.Instance[TranslationKeys.SaveToLibraryClientNotAvailable]
                };
            }

            var dialogViewModel = new SaveToLibraryDialogViewModel(client, seed);
            await dialogViewModel.InitializeAsync().ConfigureAwait(true);

            var dialog = new SaveToLibraryDialog(dialogViewModel)
            {
                Owner = Application.Current?.MainWindow
            };

            dialogViewModel.CloseRequested += accepted =>
            {
                dialog.DialogResult = accepted;
                dialog.Close();
            };

            var acceptedResult = dialog.ShowDialog() == true;
            if (!acceptedResult || dialogViewModel.SaveResult == null)
            {
                return new SaveToLibraryWorkflowResult
                {
                    Submitted = false
                };
            }

            return new SaveToLibraryWorkflowResult
            {
                Submitted = true,
                AddResult = dialogViewModel.SaveResult,
                LibraryId = dialogViewModel.SavedLibraryId
            };
        }
    }

    internal sealed class SaveToLibraryWorkflowResult
    {
        public bool Submitted { get; set; }

        public AddPartToLibraryResult AddResult { get; set; }

        public string LibraryId { get; set; }

        public string ErrorMessage { get; set; }
    }
}
