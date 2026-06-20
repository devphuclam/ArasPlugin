using System;
using System.IO;
using System.Windows;

namespace IdeaCadConnector.Desktop
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Idea",
                    "IdeaCadConnector");
                Directory.CreateDirectory(logDirectory);

                var logPath = Path.Combine(logDirectory, "startup-error.log");
                File.WriteAllText(logPath, ex.ToString());

                MessageBox.Show(
                    "IDEA PDM could not start.\n\n" + ex.Message + "\n\nDetails: " + logPath,
                    "IDEA PDM",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-1);
            }
        }
    }
}
