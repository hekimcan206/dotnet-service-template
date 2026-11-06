using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ServiceTemplate.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real application in-memory. Only configuration is swapped (connection string, environment);
/// no service is replaced, so the tests exercise the same composition root as production.
/// </summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("OpenApi:Enabled", "true");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        // Keep test output readable; failures still surface through assertions.
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }
}
