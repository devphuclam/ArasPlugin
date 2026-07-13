using Xunit;

namespace IdeaCadConnector.Tests
{
    [CollectionDefinition("Environment configuration process state", DisableParallelization = true)]
    public sealed class EnvironmentConfigurationCollection : ICollectionFixture<object>
    {
    }
}
