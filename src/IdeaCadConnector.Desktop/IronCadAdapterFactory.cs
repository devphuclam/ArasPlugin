using System.IO;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Desktop.Services;

namespace IdeaCadConnector.Desktop
{
    internal static class IronCadAdapterFactory
    {
        public static ICadApplicationAdapter Create(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new FileNotFoundException(
                    "IronCAD executable path is not configured. Set local.ironCadExecutablePath in the environment configuration.");

            return new IronCadExternalAdapter(executablePath);
        }

        public static ICadApplicationAdapter CreateFromSession()
        {
            string executablePath = AppSessionContext.Current.IronCadExecutablePath;
            if (string.IsNullOrWhiteSpace(executablePath))
                executablePath = ArasClientOptionsFactory.Current?.IronCadExecutablePath;

            return Create(executablePath);
        }
    }
}
