using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop
{
    /// <summary>
    /// Standalone adapter that drives IronCAD externally by launching it with a file path.
    /// This does NOT require the ICAPI add-in site and therefore works from a separate process.
    /// Metadata read/write is best-effort because we cannot query the live IronCAD document
    /// without the ICAPI add-in. For full live integration, use the IronCAD add-in instead.
    /// </summary>
    public sealed class IronCadExternalAdapter : ICadApplicationAdapter
    {
        private readonly string _ironCadExecutablePath;

        public IronCadExternalAdapter(string ironCadExecutablePath = @"C:\Program Files\IronCAD\2025\bin\IRONCAD.exe")
        {
            _ironCadExecutablePath = ironCadExecutablePath;
        }

        public string AuthoringTool
        {
            get { return CadConstants.IronCadAuthoringTool; }
        }

        public string AuthoringToolVersion
        {
            get { return LocalizationSource.Instance[TranslationKeys.IronCadExternalDisplayName]; }
        }

        /// <summary>
        /// Returns info about the most recently opened/downloaded file path, not the live IronCAD document.
        /// </summary>
        public CadDocumentInfo GetActiveDocumentInfo()
        {
            // Without ICAPI, we cannot query the live IronCAD active document.
            // Return null so the caller must supply the file path from its own state.
            return null;
        }

        public CadMetadata ReadMetadata()
        {
            // Best-effort: return authoring-tool metadata only.
            return new CadMetadata
            {
                AuthoringTool = AuthoringTool,
                AuthoringToolVersion = AuthoringToolVersion
            };
        }

        public void WriteMetadata(CadMetadata metadata)
        {
            // Metadata cannot be written to the live IronCAD document from an external process.
            // This is intentionally a no-op for the standalone app.
        }

        public Task OpenDocumentAsync(string filePath, CadOpenMode openMode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("CAD file not found.", filePath);

            var ironCadPath = ResolveIronCadExecutable();
            if (string.IsNullOrWhiteSpace(ironCadPath) || !File.Exists(ironCadPath))
                throw new FileNotFoundException("IronCAD executable not found.", ironCadPath);

            // IronCAD is not a console app, but StartInfo.UseShellExecute is kept true
            // so Windows handles the launch through the shell (supports wait/activation).
            var startInfo = new ProcessStartInfo
            {
                FileName = ironCadPath,
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = Path.GetDirectoryName(ironCadPath)
            };

            // Note: openMode (read-only vs edit) is honoured by Aras locking state, not by the
            // launch arguments. IronCAD will open the file normally. If read-only was requested,
            // the user has already been warned not to save locally.
            try
            {
                Process.Start(startInfo);
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException($"Failed to start IronCAD: {ex.Message}", ex);
            }

            return Task.FromResult(0);
        }

        private string ResolveIronCadExecutable()
        {
            if (!string.IsNullOrWhiteSpace(_ironCadExecutablePath) && File.Exists(_ironCadExecutablePath))
                return _ironCadExecutablePath;

            return null;
        }
    }
}
