using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceTemplate.Api.Domain;
using ServiceTemplate.Api.Features.Orders;
using ServiceTemplate.IntegrationTests.Infrastructure;

namespace ServiceTemplate.IntegrationTests;

public sealed class OrdersApiTests(PostgresFixture postgres) : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly ApiFactory _factory = new(postgres.AppConnectionString);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Full_lifecycle_create_add_merge_submit_cancel()
    {
        var client = _factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest("TABLE-12"), Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await created.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct);
        created.Headers.Location!.ToString().Should().Be($"/api/v1/orders/{order!.Id}");

        await PostOk(client, $"/api/v1/orders/{order.Id}/lines", new AddOrderLineRequest("latte-l", 2, 4.50m));
        await PostOk(client, $"/api/v1/orders/{order.Id}/lines", new AddOrderLineRequest("LATTE-L", 1, 4.50m));
        await PostOk(client, $"/api/v1/orders/{order.Id}/lines", new AddOrderLineRequest("CROISSANT", 3, 2.25m));

        var submitted = await PostOk(client, $"/api/v1/orders/{order.Id}/submit", body: null);
        submitted.Status.Should().Be(OrderStatus.Submitted);
        submitted.Total.Should().Be(20.25m);
        submitted.Lines.Should().BeEquivalentTo(
        [
            new OrderLineResponse("LATTE-L", 3, 4.50m, 13.50m),
            new OrderLineResponse("CROISSANT", 3, 2.25m, 6.75m),
        ]);

        var fetched = await client.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{order.Id}", Json, Ct);
        fetched.Should().BeEquivalentTo(submitted);

        var cancelled = await PostOk(client, $"/api/v1/orders/{order.Id}/cancel", body: null);
        cancelled.Status.Should().Be(OrderStatus.Cancelled);

        var again = await client.PostAsync($"/api/v1/orders/{order.Id}/cancel", null, Ct);
        await again.ShouldBeProblem(HttpStatusCode.Conflict, "order.already_cancelled", Ct);
    }

    [Fact]
    public async Task Request_validation_returns_field_errors()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(""), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("errors").EnumerateObject()
            .Select(p => p.Name).Should().ContainMatch("*ustomerReference");
    }

    [Fact]
    public async Task Domain_rules_beyond_annotations_are_enforced()
    {
        var client = _factory.CreateClient();
        var order = await CreateOrder(client);

        // Passes the [Range] annotation, but the domain refuses silent rounding of a third decimal.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/lines", new AddOrderLineRequest("AB-1", 1, 1.005m), Ct);

        await response.ShouldBeProblem(HttpStatusCode.BadRequest, "order.line_price_invalid", Ct);
    }

    [Fact]
    public async Task Submitting_an_empty_order_is_a_conflict()
    {
        var client = _factory.CreateClient();
        var order = await CreateOrder(client);

        var response = await client.PostAsync($"/api/v1/orders/{order.Id}/submit", null, Ct);

        await response.ShouldBeProblem(HttpStatusCode.Conflict, "order.empty", Ct);
    }

    [Fact]
    public async Task Unknown_order_returns_problem_details_with_trace_id()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/orders/{Guid.CreateVersion7()}", Ct);

        using var problem = await response.ShouldBeProblem(HttpStatusCode.NotFound, "order.not_found", Ct);
        problem.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Concurrent_additions_never_lose_an_update()
    {
        var client = _factory.CreateClient();
        var order = await CreateOrder(client);
        const int requests = 20;

        // All requests race for the same row. Each must either apply fully (200) or be rejected with a
        // retryable 409; a 500 or a silently lost increment would both be bugs.
        var responses = await Task.WhenAll(Enumerable.Range(0, requests).Select(_ =>
            client.PostAsJsonAsync($"/api/v1/orders/{order.Id}/lines", new AddOrderLineRequest("ESPRESSO", 1, 2.00m), Ct)));

        responses.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.OK || s == HttpStatusCode.Conflict);
        var applied = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        applied.Should().BeGreaterThan(0);

        var final = await client.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{order.Id}", Json, Ct);
        final!.Lines.Should().ContainSingle().Which.Quantity.Should().Be(applied);
    }

    [Fact]
    public async Task Openapi_document_describes_versioned_endpoints()
    {
        var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", Ct));

        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.Should().Contain(["/api/v1/orders", "/api/v1/orders/{id}", "/api/v1/orders/{id}/lines"]);
        paths.Should().NotContain(p => p.StartsWith("/health", StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static async Task<OrderResponse> CreateOrder(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest("TABLE-1"), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
    }

    private static async Task<OrderResponse> PostOk(HttpClient client, string url, object? body)
    {
        var response = body is null ? await client.PostAsync(url, null, Ct) : await client.PostAsJsonAsync(url, body, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Json, Ct))!;
    }
}
