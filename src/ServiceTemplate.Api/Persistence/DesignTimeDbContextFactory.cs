using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ServiceTemplate.Api.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without starting the web host or reaching a database.
/// The connection string is a placeholder; it is never opened at design time.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time", PersistenceExtensions.ConfigureNpgsql)
            .Options;
        return new AppDbContext(options);
    }
}
