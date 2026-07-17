using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    internal sealed class IronCadNativeSaveAllExternalInvoker
    {
        // IronCAD 2025 ISEngineAfx.dll resource/WM_COMMAND ID for
        // "Save All Part/Assembly As External".
        private const int CommandId = 53046;
        private const uint WmCommand = 0x0111;
        private const uint BffmSetSelectionW = 0x0400 + 103;
        private const uint BmClick = 0x00F5;

        public void Execute(string destinationDirectory)
        {
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ArgumentException("Destination directory is required.", nameof(destinationDirectory));

            var process = Process.GetCurrentProcess();
            process.Refresh();
            var mainWindow = process.MainWindowHandle;
            if (mainWindow == IntPtr.Zero)
                mainWindow = FindIronCadMainWindow((uint)process.Id);
            if (mainWindow == IntPtr.Zero)
                throw new InvalidOperationException("IRONCAD_MAIN_WINDOW_UNAVAILABLE");

            Exception dialogFailure = null;
            var dialogHandled = new ManualResetEventSlim(false);
            var dialogThread = new Thread(() =>
            {
                IntPtr dialog = IntPtr.Zero;
                try
                {
                    dialog = WaitForFolderDialog((uint)process.Id, TimeSpan.FromSeconds(15));
                    if (dialog == IntPtr.Zero)
                        throw new InvalidOperationException("SAVE_ALL_EXTERNAL_FOLDER_DIALOG_UNAVAILABLE");

                    SendMessage(dialog, BffmSetSelectionW, new IntPtr(1), destinationDirectory);
                    Thread.Sleep(250);
                    var okButton = FindWindowEx(dialog, IntPtr.Zero, "Button", "OK");
                    if (okButton == IntPtr.Zero)
                        throw new InvalidOperationException("SAVE_ALL_EXTERNAL_OK_BUTTON_UNAVAILABLE");
                    SendMessage(okButton, BmClick, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    dialogFailure = ex;
                    if (dialog != IntPtr.Zero)
                    {
                        var cancelButton = FindWindowEx(dialog, IntPtr.Zero, "Button", "Cancel");
                        if (cancelButton != IntPtr.Zero)
                            SendMessage(cancelButton, BmClick, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                finally
                {
                    dialogHandled.Set();
                }
            });
            dialogThread.IsBackground = true;
            dialogThread.Name = "IronCAD Save All As External folder selector";
            dialogThread.Start();

            // Invoke the exact MFC command used by the ribbon button. The call
            // stays synchronous while IronCAD exports all definitions; the
            // worker above only supplies the destination to its modal dialog.
            SendMessage(mainWindow, WmCommand, new IntPtr(CommandId), IntPtr.Zero);

            if (!dialogHandled.Wait(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("SAVE_ALL_EXTERNAL_DIALOG_DID_NOT_COMPLETE");
            if (dialogFailure != null)
                throw new InvalidOperationException("SAVE_ALL_EXTERNAL_DIALOG_FAILED", dialogFailure);
        }

        private static IntPtr WaitForFolderDialog(uint processId, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var dialog = FindTopLevelWindow(processId, (className, title) =>
                    string.Equals(className, "#32770", StringComparison.Ordinal) &&
                    title.IndexOf("Browse For Folder", StringComparison.OrdinalIgnoreCase) >= 0);
                if (dialog != IntPtr.Zero) return dialog;
                Thread.Sleep(50);
            }
            return IntPtr.Zero;
        }

        private static IntPtr FindIronCadMainWindow(uint processId)
        {
            return FindTopLevelWindow(processId, (className, title) =>
                !string.Equals(className, "#32770", StringComparison.Ordinal) &&
                title.StartsWith("IRONCAD", StringComparison.OrdinalIgnoreCase));
        }

        private static IntPtr FindTopLevelWindow(uint processId, Func<string, string, bool> predicate)
        {
            var result = IntPtr.Zero;
            EnumWindows((window, _) =>
            {
                uint windowProcessId;
                GetWindowThreadProcessId(window, out windowProcessId);
                if (windowProcessId != processId || !IsWindowVisible(window)) return true;

                var className = new StringBuilder(256);
                var title = new StringBuilder(512);
                GetClassName(window, className, className.Capacity);
                GetWindowText(window, title, title.Capacity);
                if (!predicate(className.ToString(), title.ToString())) return true;
                result = window;
                return false;
            }, IntPtr.Zero);
            return result;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, string lParam);
    }
}
