using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceTemplate.Api.Domain;
using ServiceTemplate.IntegrationTests.Infrastructure;

namespace ServiceTemplate.IntegrationTests;

public sealed class HealthAndInfrastructureTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Liveness_and_readiness_are_healthy_when_database_is_up()
    {
        await using var factory = new ApiFactory(postgres.AppConnectionString);
        var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live", Ct);
        var ready = await client.GetAsync("/health/ready", Ct);

        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await ready.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("checks").GetProperty("database").GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Readiness_fails_but_liveness_stays_up_when_database_is_unreachable()
    {
        // Port 1 on loopback refuses immediately: a deterministic "database down" without stopping the shared container.
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");
        var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live", Ct);
        var ready = await client.GetAsync("/health/ready", Ct);

        live.StatusCode.Should().Be(HttpStatusCode.OK, "a dependency outage must not get the process restarted");
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await ready.Content.ReadAsStringAsync(Ct);
        body.Should().Contain("Unhealthy").And.NotContain("Exception", "failure details must not leak");
    }

    [Fact]
    public async Task Runtime_role_has_dml_rights_only()
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(Ct);

        await using var drop = new NpgsqlCommand("DROP TABLE service.order_lines", connection);
        var dropAttempt = async () => await drop.ExecuteNonQueryAsync(Ct);
        (await dropAttempt.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        await using var create = new NpgsqlCommand("CREATE TABLE service.sneaky (id int)", connection);
        var createAttempt = async () => await create.ExecuteNonQueryAsync(Ct);
        (await createAttempt.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Stale_write_is_rejected_by_the_row_version()
    {
        var clock = TimeProvider.System;
        var order = Order.Create("RACE", clock).Value;
        await using (var setup = postgres.CreateAppDbContext())
        {
            setup.Orders.Add(order);
            await setup.SaveChangesAsync(Ct);
        }

        await using var first = postgres.CreateAppDbContext();
        await using var second = postgres.CreateAppDbContext();
        var mine = await first.Orders.SingleAsync(o => o.Id == order.Id, Ct);
        var theirs = await second.Orders.SingleAsync(o => o.Id == order.Id, Ct);

        theirs.AddLine("AB-1", 1, 1m, clock);
        await second.SaveChangesAsync(Ct);

        mine.AddLine("AB-2", 1, 1m, clock);
        var staleSave = async () => await first.SaveChangesAsync(Ct);

        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
