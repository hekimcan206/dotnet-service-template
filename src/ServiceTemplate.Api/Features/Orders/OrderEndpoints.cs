using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ServiceTemplate.Api.Domain;
using ServiceTemplate.Api.Infrastructure;
using ServiceTemplate.Api.Persistence;

namespace ServiceTemplate.Api.Features.Orders;

/// <summary>
/// Sample feature showing the intended shape of an endpoint: thin handler, rules in the aggregate,
/// typed results (so OpenAPI documents every status code), and domain errors mapped to ProblemDetails.
/// Replace it with your own features; the plumbing around it is what the template is for.
/// </summary>
internal static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", CreateOrderAsync)
            .WithName("CreateOrder")
            .WithSummary("Open a new draft order");

        group.MapGet("/{id:guid}", GetOrderAsync)
            .WithName("GetOrder")
            .WithSummary("Get an order by id");

        group.MapPost("/{id:guid}/lines", AddLineAsync)
            .WithName("AddOrderLine")
            .WithSummary("Add a product line to a draft order")
            .WithDescription("Adding a SKU that is already on the order increases its quantity; the unit price must match.");

        group.MapPost("/{id:guid}/submit", SubmitAsync)
            .WithName("SubmitOrder")
            .WithSummary("Submit a non-empty draft order");

        group.MapPost("/{id:guid}/cancel", CancelAsync)
            .WithName("CancelOrder")
            .WithSummary("Cancel a draft or submitted order");

        return group;
    }

    internal static async Task<Results<Created<OrderResponse>, ProblemHttpResult>> CreateOrderAsync(
        CreateOrderRequest request, AppDbContext db, TimeProvider clock, OrderMetrics metrics, CancellationToken cancellationToken)
    {
        var result = Order.Create(request.CustomerReference, clock);
        if (!result.IsSuccess)
        {
            return ToProblem(result.Error);
        }

        var order = result.Value;
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        metrics.OrderCreated();
        Activity.Current?.SetTag("order.id", order.Id);
        return TypedResults.Created($"/api/v1/orders/{order.Id}", OrderResponse.From(order));
    }

    internal static async Task<Results<Ok<OrderResponse>, ProblemHttpResult>> GetOrderAsync(
        Guid id, AppDbContext db, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        return order is null ? ToProblem(OrderNotFound(id)) : TypedResults.Ok(OrderResponse.From(order));
    }

    internal static Task<Results<Ok<OrderResponse>, ProblemHttpResult>> AddLineAsync(
        Guid id, AddOrderLineRequest request, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        MutateAsync(id, db, order => order.AddLine(request.Sku, request.Quantity, request.UnitPrice, clock), cancellationToken);

    internal static Task<Results<Ok<OrderResponse>, ProblemHttpResult>> SubmitAsync(
        Guid id, AppDbContext db, TimeProvider clock, OrderMetrics metrics, CancellationToken cancellationToken) =>
        MutateAsync(
            id,
            db,
            order =>
            {
                var result = order.Submit(clock);
                if (result.IsSuccess)
                {
                    metrics.OrderSubmitted(order.Total);
                }

                return result;
            },
            cancellationToken);

    internal static Task<Results<Ok<OrderResponse>, ProblemHttpResult>> CancelAsync(
        Guid id, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        MutateAsync(id, db, order => order.Cancel(clock), cancellationToken);

    /// <summary>
    /// Load → apply one domain operation → save. A concurrent writer is detected by the xmin row version at
    /// SaveChanges and surfaces as 409 via <see cref="ConcurrencyConflictExceptionHandler"/>.
    /// </summary>
    private static async Task<Results<Ok<OrderResponse>, ProblemHttpResult>> MutateAsync(
        Guid id, AppDbContext db, Func<Order, Result> operation, CancellationToken cancellationToken)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return ToProblem(OrderNotFound(id));
        }

        Activity.Current?.SetTag("order.id", order.Id);
        var result = operation(order);
        if (!result.IsSuccess)
        {
            return ToProblem(result.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(OrderResponse.From(order));
    }

    private static DomainError OrderNotFound(Guid id) =>
        DomainError.NotFound("order.not_found", $"Order {id} does not exist.");

    private static ProblemHttpResult ToProblem(DomainError error) => TypedResults.Problem(
        statusCode: error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => throw new UnreachableException($"Unmapped error kind {error.Kind}."),
        },
        title: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
