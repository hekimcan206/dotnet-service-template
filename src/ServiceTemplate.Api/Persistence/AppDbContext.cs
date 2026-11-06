using Microsoft.EntityFrameworkCore;
using ServiceTemplate.Api.Domain;

namespace ServiceTemplate.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// All objects live in a dedicated schema owned by the migrator role; the runtime role only gets DML
    /// rights on it (see deploy/postgres/init).
    /// </summary>
    public const string Schema = "service";

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
