using Xunit;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresTestcontainerFixture>
{
    public const string Name = "postgres-testcontainer";
}
