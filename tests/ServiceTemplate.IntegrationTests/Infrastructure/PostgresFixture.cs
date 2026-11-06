using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceTemplate.Api.Persistence;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(ServiceTemplate.IntegrationTests.Infrastructure.PostgresFixture))]

namespace ServiceTemplate.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL container for the whole test assembly (start-up cost is paid once). It is initialised
/// with the same role script as docker compose, migrated as <c>service_migrator</c>, and the API under test
/// connects as <c>service_app</c>, mirroring production privileges.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Test-only credentials for a throwaway container; never used outside this process.
    private const string MigratorPassword = "migrator-test";
    private const string AppPassword = "app-test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("servicetemplate")
        .WithEnvironment("MIGRATOR_PASSWORD", MigratorPassword)
        .WithEnvironment("APP_PASSWORD", AppPassword)
        .WithResourceMapping(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "postgres-init", "01-roles.sh")),
            "/docker-entrypoint-initdb.d/01-roles.sh")
        .Build();

    public string MigratorConnectionString => WithCredentials("service_migrator", MigratorPassword);

    public string AppConnectionString => WithCredentials("service_app", AppPassword);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(MigratorConnectionString, PersistenceExtensions.ConfigureNpgsql)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public AppDbContext CreateAppDbContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(AppConnectionString, PersistenceExtensions.ConfigureNpgsql)
            .Options);

    private string WithCredentials(string username, string password) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = username,
            Password = password,
        }.ConnectionString;
}
