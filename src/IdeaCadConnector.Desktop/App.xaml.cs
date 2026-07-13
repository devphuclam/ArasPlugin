using System;
using System.Globalization;
using System.IO;
using System.Windows;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Desktop.Services;

namespace IdeaCadConnector.Desktop
{
    public partial class App : Application
    {
        internal static ArasClientOptions LoadedOptions => ArasClientOptionsFactory.Current;

        internal static Core.Configuration.EnvironmentConfigurationResult ConfigLoadResult => ArasClientOptionsFactory.CurrentConfig;

        protected override void OnStartup(StartupEventArgs e)
        {
            var savedLanguage = SettingsService.LoadLanguage();
            if (!string.IsNullOrWhiteSpace(savedLanguage))
            {
                try
                {
                    var culture = new CultureInfo(savedLanguage);
                    CultureInfo.CurrentUICulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
                catch
                {
                }
            }

            ArasClientOptionsFactory.Initialize();

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

                var title = TranslationResources.GetString(
                    CultureInfo.CurrentUICulture.Name, TranslationKeys.StartupErrorTitle);
                var message = TranslationResources.GetString(
                    CultureInfo.CurrentUICulture.Name, TranslationKeys.StartupErrorMessage);

                MessageBox.Show(
                    message + "\n\n" + ex.Message + "\n\nDetails: " + logPath,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-1);
            }
        }
    }
}
