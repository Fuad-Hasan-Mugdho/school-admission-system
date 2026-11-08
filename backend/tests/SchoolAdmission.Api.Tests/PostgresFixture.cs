using Testcontainers.PostgreSql;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Provides a PostgreSQL database for the whole test run.
/// - If TEST_DATABASE_URL is set (e.g. inside docker compose) that database is used as-is.
/// - Otherwise a throw-away postgres:16 container is started through Testcontainers (needs Docker).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(external))
        {
            ConnectionString = external;
            return;
        }

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("admission_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
