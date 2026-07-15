using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Desktop.Services;

namespace IdeaCadConnector.Desktop
{
    internal static class IronCadAdapterFactory
    {
        public static ICadApplicationAdapter Create(string executablePath)
        {
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
