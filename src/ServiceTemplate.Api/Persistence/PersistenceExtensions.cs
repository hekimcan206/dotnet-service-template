using Microsoft.EntityFrameworkCore;

namespace ServiceTemplate.Api.Persistence;

internal static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it via appsettings, user secrets or the ConnectionStrings__Default environment variable.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString, ConfigureNpgsql));
        return services;
    }

    internal static void ConfigureNpgsql(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("__ef_migrations_history", AppDbContext.Schema);

    /// <summary>
    /// Applies pending migrations. Run by the dedicated <c>--migrate-only</c> process in containers
    /// (with the schema-owner role), or at startup in local development when Database:MigrateOnStartup is true.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
