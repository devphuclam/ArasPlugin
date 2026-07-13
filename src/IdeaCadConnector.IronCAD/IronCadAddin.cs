using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using interop.ICApiIronCAD;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Ui.Views;
using IdeaCadConnector.Workspace;

namespace IdeaCadConnector.IronCAD
{
    [Guid("B1A006AC-1386-4811-AA71-8CF55414ACEF")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("IdeaCadConnector.IronCAD.AddIn")]
    public sealed class IronCadAddin : IZAddinServer
    {
        private static readonly string AddInDirectory;
        private static readonly string HiddenAsmDir;

        static IronCadAddin()
        {
            var asmLocation = Assembly.GetExecutingAssembly().Location;
            AddInDirectory = string.IsNullOrEmpty(asmLocation)
                ? System.IO.Path.GetDirectoryName(typeof(IronCadAddin).Assembly.Location)
                  ?? AppDomain.CurrentDomain.BaseDirectory
                : System.IO.Path.GetDirectoryName(asmLocation);

            HiddenAsmDir = System.IO.Path.Combine(AddInDirectory, ".hid");

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            PreLoadHiddenAssemblies();
            PreLoadDependencyAssemblies();
        }

        private static void PreLoadHiddenAssemblies()
        {
            if (!System.IO.Directory.Exists(HiddenAsmDir))
                return;

            foreach (var dllPath in System.IO.Directory.GetFiles(HiddenAsmDir, "*.dll"))
            {
                try { Assembly.Load(System.IO.File.ReadAllBytes(dllPath)); }
                catch { }
            }
        }

        /// <summary>
        /// Pre-loads all dependency assemblies from the add-in directory via byte[]
        /// so the CLR can resolve them even when a strong-named assembly (our add-in)
        /// references a non-strong-named one (Core, Aras, Workspace, Ui).
        /// Loading via byte[] bypasses strong-name verification entirely.
        /// </summary>
        private static void PreLoadDependencyAssemblies()
        {
            var deps = new[] {
                "IdeaCadConnector.Core.dll",
                "IdeaCadConnector.Aras.dll",
                "IdeaCadConnector.Workspace.dll",
                "IdeaCadConnector.Ui.dll"
            };

            foreach (var dllName in deps)
            {
                var path = System.IO.Path.Combine(AddInDirectory, dllName);
                if (System.IO.File.Exists(path))
                {
                    try { Assembly.Load(System.IO.File.ReadAllBytes(path)); }
                    catch { }
                }
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            var dllName = name + ".dll";

            var hiddenPath = System.IO.Path.Combine(HiddenAsmDir, dllName);
            if (System.IO.File.Exists(hiddenPath))
            {
                try { return Assembly.Load(System.IO.File.ReadAllBytes(hiddenPath)); }
                catch { }
            }

            var dirPath = System.IO.Path.Combine(AddInDirectory, dllName);
            if (System.IO.File.Exists(dirPath))
            {
                try { return Assembly.Load(System.IO.File.ReadAllBytes(dirPath)); }
                catch { }
            }

            return null;
        }

        // ---- Instance members ------------------------------------------------

        private ZAddinSite _addinSite;

        private const int ButtonCount = 6;
        private ZCommandHandler[] _buttons = new ZCommandHandler[ButtonCount];
        private string[] _buttonIds = { "IdeaPdm_Login", "IdeaPdm_SearchPart", "IdeaPdm_Checkout", "IdeaPdm_OpenReadOnly", "IdeaPdm_Checkin", "IdeaPdm_CancelCheckout" };

        private ArasCadClient _arasClient;
        private IronCadCadAdapter _cadAdapter;
        private ArasLoginResult _loginResult;
        private WorkspaceService _workspaceService;

        private string _selectedPartId;
        private string _selectedCadId;
        private string _lockToken;
        private bool _hasNativeFile;
        private bool _cadLockedByOtherUser;
        private string _currentCadState;

        // ---- IZAddinServer ---------------------------------------------------

        public void InitSelf(ZAddinSite piAddinSite)
        {
            if (piAddinSite == null)
            {
                MessageBox.Show("Addin Server is null.");
                return;
            }

            _addinSite = piAddinSite;
            _cadAdapter = new IronCadCadAdapter(this);
            _workspaceService = new WorkspaceService(new WorkspaceOptions());

            try
            {
                var cEnvMgr = IronCADApp.EnvironmentMgr;
                var cEnv = cEnvMgr.get_Environment(eZEnvType.Z_ENV_SCENE);
                var cRibbonBar = cEnv.GetRibbonBar(eZRibbonBarType.Z_RIBBONBAR);
                var cControlBar = cEnv.AddControlBar(piAddinSite, "IDEA PDM");
                var cControls = cControlBar.Controls;

                string[] names = { "Login", "Search Part", "Checkout", "Open Read-Only", "Check-in", "Cancel Checkout" };
                string[] descs = {
                    "Login to Aras Innovator",
                    "Search and select Part from Aras",
                    "Checkout CAD for editing",
                    "Open CAD file without locking",
                    "Check-in CAD to Aras",
                    "Release lock without checking in"
                };

                for (int i = 0; i < ButtonCount; i++)
                {
                    // Create button command handler
                    _buttons[i] = piAddinSite.CreateCommandHandler(
                        _buttonIds[i], names[i], descs[i], descs[i], null, null);
                    _buttons[i].Enabled = true;

                    // Add to control bar
                    cControls.Add(ezControlType.Z_CONTROL_BUTTON, _buttons[i].ControlDescriptor, null);

                    // Add to ribbon bar
                    cRibbonBar.AddButton(_buttons[i].ControlDescriptor);

                    // Wire event handlers
                    var idx = i; // capture for closure
                    _buttons[i].OnClick += () => OnButtonClick(idx);
                    _buttons[i].OnUpdate += UpdateButtonStates;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing add-in: " + ex.Message);
            }
        }

        public void DeInitSelf()
        {
            for (int i = 0; i < ButtonCount; i++)
            {
                _buttons[i] = null;
            }

            if (_arasClient != null)
            {
                _arasClient.Dispose();
                _arasClient = null;
            }

            _cadAdapter = null;
            _addinSite = null;
        }

        // ---- Properties ------------------------------------------------------

        public IZBaseApp IronCADApp
        {
            get { return _addinSite?.Application; }
        }

        internal ZAddinSite AddinSite
        {
            get { return _addinSite; }
        }

#if DEBUG
        /// <summary>
        /// Developer-only, read-only diagnostic seam. It is not registered as a user command.
        /// </summary>
        internal string RunBomDiagnosticProbe(string outputFolder, string reportName)
        {
            var result = new BomDiagnostic.IronCadBomDiagnosticProbe().Run(
                IronCADApp,
                outputFolder,
                reportName);
            return result.ReportPath;
        }
#endif

        // ---- Button states ----------------------------------------------------

        private void UpdateButtonStates()
        {
            bool loggedIn = _arasClient != null && _loginResult != null;
            bool hasCad = !string.IsNullOrWhiteSpace(_selectedCadId);
            bool hasLock = !string.IsNullOrWhiteSpace(_lockToken);
            bool editableState = CadLifecyclePolicy.CanCheckout(_currentCadState);
            bool canCheckout = loggedIn
                && hasCad
                && editableState
                && !hasLock
                && !_cadLockedByOtherUser;

            // Index: 0=Login, 1=Search Part, 2=Checkout, 3=Open Read-Only, 4=Check-in, 5=Cancel Checkout
            for (int i = 0; i < ButtonCount; i++)
            {
                if (_buttons[i] == null) continue;

                _buttons[i].Enabled = i switch
                {
                    0 => true,                                        // Login: always
                    1 => loggedIn,                                    // Search: only when logged in
                    2 => canCheckout,                                 // Checkout: editable state + not locked by someone else
                    3 => loggedIn && hasCad && _hasNativeFile,        // Open RO: logged in + CAD + file exists
                    4 => loggedIn && hasLock,                         // Check-in: only when checked out
                    5 => loggedIn && hasLock,                         // Cancel CO: only when checked out
                    _ => false
                };
            }
        }

        private void UpdateCadStateFlags(CadSummary cad)
        {
            _currentCadState = cad?.State;
            _hasNativeFile = cad != null && cad.HasNativeFile;
            _cadLockedByOtherUser = cad != null
                && cad.IsLocked
                && string.IsNullOrWhiteSpace(_lockToken);
        }

        // ---- Button handlers -------------------------------------------------

        private void OnButtonClick(int index)
        {
            switch (index)
            {
                case 0: LoginButton_OnExecute(); break;
                case 1: SearchButton_OnExecute(); break;
                case 2: CheckoutButton_OnExecute(); break;
                case 3: OpenReadOnlyButton_OnExecute(); break;
                case 4: CheckinButton_OnExecute(); break;
                case 5: CancelCheckoutButton_OnExecute(); break;
            }
        }

        private void LoginButton_OnExecute()
        {
            try
            {
                ArasClientOptionsFactory.Initialize();
                var options = ArasClientOptionsFactory.Current;
                var dialog = new LoginDialog(options);
                dialog.ShowDialog();

                if (dialog.ViewModel.IsConnected && dialog.LoginRequest != null)
                {
                    if (_arasClient != null) _arasClient.Dispose();

                    var mergedOptions = options.WithLoginOverrides(
                        dialog.LoginRequest.ServerUrl, dialog.LoginRequest.Database);
                    _arasClient = new ArasCadClient(mergedOptions);
                    _loginResult = _arasClient.LoginAsync(
                        dialog.LoginRequest, System.Threading.CancellationToken.None)
                        .GetAwaiter().GetResult();

                    UpdateButtonStates();

                    MessageBox.Show(
                        "Connected to Aras Innovator.\nUser: " + _loginResult.UserName +
                        "\nDatabase: " + _loginResult.Database,
                        "IDEA PDM - Login",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Login error: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool EnsureLoggedIn()
        {
            if (_arasClient == null || _loginResult == null)
            {
                MessageBox.Show("Please login first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void SearchButton_OnExecute()
        {
            if (!EnsureLoggedIn()) return;

            try
            {
                var keyword = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter Part number or name to search:", "IDEA PDM - Search Part", "");

                var searchRequest = new PartSearchRequest { Keyword = keyword, MaxResults = 20 };
                var response = _arasClient.SearchPartsAsync(searchRequest, System.Threading.CancellationToken.None)
                    .GetAwaiter().GetResult();
                var results = response.Items;

                if (results.Count == 0)
                {
                    MessageBox.Show("No Parts found.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var list = new System.Text.StringBuilder();
                for (int i = 0; i < results.Count; i++)
                {
                    var p = results[i].Part;
                    list.AppendLine((i + 1) + ". " + p.PartNumber + " - " + p.Name +
                        (results[i].IronCadPartCad != null ? " [CAD: " + results[i].IronCadPartCad.CadNumber + "]" : ""));
                }

                var indexStr = Microsoft.VisualBasic.Interaction.InputBox(
                    list.ToString() + "\n\nEnter number to select:", "IDEA PDM - Select Part", "1");

                int index;
                if (!int.TryParse(indexStr, out index) || index < 1 || index > results.Count)
                    return;

                var selected = results[index - 1];
                _selectedPartId = selected.Part.Id;

                var ensureResult = _arasClient.CreateCadAsync(
                    new CreateCadRequest { PartId = _selectedPartId },
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                _selectedCadId = ensureResult.Cad.Id;
                _lockToken = null;
                UpdateCadStateFlags(ensureResult.Cad);

                UpdateButtonStates();

                MessageBox.Show(
                    "Selected: " + selected.Part.PartNumber + "\nCAD: " + ensureResult.Cad.CadNumber +
                    "\nState: " + ensureResult.Cad.State,
                    "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Search error: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckoutButton_OnExecute()
        {
            if (!EnsureLoggedIn()) return;
            if (string.IsNullOrWhiteSpace(_selectedCadId))
            {
                MessageBox.Show("Search and select a Part first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = _arasClient.CheckoutAsync(
                    new CadCheckoutRequest { CadId = _selectedCadId },
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                _lockToken = result.LockToken;
                UpdateCadStateFlags(result.Cad);

                UpdateButtonStates();

                if (result.Cad.HasNativeFile)
                {
                    var targetDir = _workspaceService.GetCadPartPath(_selectedPartId);
                    targetDir = System.IO.Path.GetDirectoryName(targetDir);

                    var downloadedPath = _arasClient.DownloadNativeFileAsync(
                        result.Cad.NativeFileId, targetDir,
                        System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                    // Try to open the file in IronCAD (best-effort)
                    _cadAdapter.OpenDocumentAsync(downloadedPath, Core.Cad.CadOpenMode.Edit,
                        System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                    MessageBox.Show("Checked out: " + downloadedPath,
                        "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Checked out (locked). No native file yet.",
                        "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Checkout error: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenReadOnlyButton_OnExecute()
        {
            if (!EnsureLoggedIn()) return;
            if (string.IsNullOrWhiteSpace(_selectedCadId))
            {
                MessageBox.Show("Search and select a Part first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = _arasClient.OpenReadOnlyAsync(
                    new CadOpenReadOnlyRequest { CadId = _selectedCadId },
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                UpdateCadStateFlags(result.Cad);

                if (!result.Cad.HasNativeFile)
                {
                    MessageBox.Show("CAD has no native file to open.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var targetDir = _workspaceService.GetCadPartPath(_selectedPartId);
                targetDir = System.IO.Path.GetDirectoryName(targetDir);

                var downloadedPath = _arasClient.DownloadNativeFileAsync(
                    result.Cad.NativeFileId, targetDir,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                _cadAdapter.OpenDocumentAsync(downloadedPath, Core.Cad.CadOpenMode.ReadOnly,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                MessageBox.Show("Downloaded: " + downloadedPath +
                    "\n\nOpen it manually in IronCAD.",
                    "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Open read-only error: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckinButton_OnExecute()
        {
            if (!EnsureLoggedIn()) return;
            if (string.IsNullOrWhiteSpace(_selectedCadId) || string.IsNullOrWhiteSpace(_lockToken))
            {
                MessageBox.Show("Checkout a Part first before checking in.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var docInfo = _cadAdapter.GetActiveDocumentInfo();
                if (docInfo == null)
                {
                    MessageBox.Show("No active document to check in.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var uploadResult = _arasClient.UploadFileAsync(
                    new FileUploadRequest
                    {
                        FilePath = docInfo.FullPath,
                        FileName = docInfo.FileName
                    },
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                var checkinRequest = CadCheckinRequest.CreateNew();
                checkinRequest.CadId = _selectedCadId;
                checkinRequest.LockToken = _lockToken;
                checkinRequest.UploadedFileId = uploadResult.UploadedFileId;
                checkinRequest.LocalFilePath = docInfo.FullPath;
                checkinRequest.Metadata = _cadAdapter.ReadMetadata();

                var checkinResult = _arasClient.CheckinAsync(checkinRequest,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                _lockToken = null;
                UpdateCadStateFlags(checkinResult.Cad);

                UpdateButtonStates();

                MessageBox.Show(
                    "Check-in successful.\nCAD: " + checkinResult.Cad.CadNumber +
                    "\nHas File: " + checkinResult.Cad.HasNativeFile,
                    "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Check-in error: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelCheckoutButton_OnExecute()
        {
            if (!EnsureLoggedIn()) return;
            if (string.IsNullOrWhiteSpace(_selectedCadId))
            {
                MessageBox.Show("No active checkout to cancel.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = _arasClient.CancelCheckoutAsync(
                    new CancelCheckoutRequest { CadId = _selectedCadId },
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                _lockToken = null;
                _cadLockedByOtherUser = false;

                UpdateButtonStates();

                MessageBox.Show("Checkout cancelled.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cancel checkout error: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
