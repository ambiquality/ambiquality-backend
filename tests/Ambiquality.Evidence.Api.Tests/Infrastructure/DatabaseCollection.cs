using Xunit;

namespace Ambiquality.Evidence.Api.Tests.Infrastructure;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
}
