using System.ComponentModel.DataAnnotations;
using ServiceTemplate.Api.Domain;

namespace ServiceTemplate.Api.Features.Orders;

// Wire contracts are separate from the domain model so the database shape, the domain and the public API
// can evolve independently. DataAnnotations give fast 400s with field-level errors at the edge; the domain
// still enforces every rule itself.

/// <summary>Request to open a new draft order.</summary>
/// <param name="CustomerReference">Caller-supplied reference, e.g. a customer or table number.</param>
public sealed record CreateOrderRequest(
    [Required, StringLength(Order.MaxCustomerReferenceLength, MinimumLength = 1)] string CustomerReference);

/// <summary>Request to add a product line (or more of an existing SKU) to a draft order.</summary>
/// <param name="Sku">Product code: 3-32 characters of A-Z, 0-9 or '-'. Case-insensitive.</param>
/// <param name="Quantity">Units to add.</param>
/// <param name="UnitPrice">Price per unit, at most two decimals.</param>
public sealed record AddOrderLineRequest(
    [Required, StringLength(32, MinimumLength = 3)] string Sku,
    [Range(1, Order.MaxQuantityPerLine)] int Quantity,
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal UnitPrice);

/// <summary>An order line as returned by the API.</summary>
public sealed record OrderLineResponse(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

/// <summary>An order as returned by the API.</summary>
public sealed record OrderResponse(
    Guid Id,
    string CustomerReference,
    OrderStatus Status,
    decimal Total,
    IReadOnlyList<OrderLineResponse> Lines,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CancelledAt)
{
    public static OrderResponse From(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new(
            order.Id,
            order.CustomerReference,
            order.Status,
            order.Total,
            [.. order.Lines.Select(l => new OrderLineResponse(l.Sku, l.Quantity, l.UnitPrice, l.LineTotal))],
            order.CreatedAt,
            order.SubmittedAt,
            order.CancelledAt);
    }
}
