using System;

namespace IdeaCadConnector.Core.Configuration
{
    internal sealed class EnvironmentConfigurationPathContext
    {
        public EnvironmentConfigurationPathContext(
            string environmentVariableValue,
            string sideBySideDirectory,
            string appDataDirectory)
        {
            EnvironmentVariableValue = environmentVariableValue;
            SideBySideDirectory = sideBySideDirectory ?? throw new ArgumentNullException(nameof(sideBySideDirectory));
            AppDataDirectory = appDataDirectory ?? throw new ArgumentNullException(nameof(appDataDirectory));
        }

        public string EnvironmentVariableValue { get; }

        public string SideBySideDirectory { get; }

        public string AppDataDirectory { get; }
    }
}
