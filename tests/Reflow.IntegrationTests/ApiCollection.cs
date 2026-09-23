using Xunit;

namespace Reflow.IntegrationTests;

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ReflowApiFactory>
{
}
