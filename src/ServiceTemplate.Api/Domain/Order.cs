using System.Text.RegularExpressions;

namespace ServiceTemplate.Api.Domain;

/// <summary>
/// Aggregate root for a customer order. All invariants live here so they hold no matter which
/// endpoint, job or test drives the object:
/// <list type="bullet">
/// <item>lines can only change while the order is a draft;</item>
/// <item>a SKU appears at most once; adding it again merges quantities, but only at the same unit price;</item>
/// <item>quantities and prices stay within bounds, and prices have at most two decimals (no silent rounding);</item>
/// <item>only a non-empty draft can be submitted; a cancelled order is final.</item>
/// </list>
/// </summary>
public sealed partial class Order
{
    public const int MaxLines = 50;
    public const int MaxQuantityPerLine = 1_000;
    public const int MaxCustomerReferenceLength = 64;
    public const decimal MaxUnitPrice = 1_000_000m;

    private readonly List<OrderLine> _lines = [];

    // Required by EF Core for materialisation.
    private Order()
    {
        CustomerReference = string.Empty;
    }

    private Order(Guid id, string customerReference, DateTimeOffset now)
    {
        Id = id;
        CustomerReference = customerReference;
        Status = OrderStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string CustomerReference { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Touched by every mutation. Besides auditing, this forces an UPDATE of the order row when only
    /// lines change, so the row-version concurrency check also protects line edits.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    public decimal Total => _lines.Sum(l => l.LineTotal);

    public static Result<Order> Create(string customerReference, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var reference = customerReference?.Trim() ?? string.Empty;
        if (reference.Length is 0 or > MaxCustomerReferenceLength)
        {
            return Result.Failure<Order>(DomainError.Validation(
                "order.customer_reference_invalid",
                $"Customer reference must be between 1 and {MaxCustomerReferenceLength} characters."));
        }

        // Version 7 GUIDs are time-ordered, which keeps the primary-key B-tree append-mostly.
        var now = Now(clock);
        return Result.Success(new Order(Guid.CreateVersion7(now), reference, now));
    }

    public Result AddLine(string sku, int quantity, decimal unitPrice, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != OrderStatus.Draft)
        {
            return NotDraft("Lines can only be added to a draft order.");
        }

        var normalisedSku = sku?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SkuPattern().IsMatch(normalisedSku))
        {
            return Result.Failure(DomainError.Validation(
                "order.line_sku_invalid", "SKU must be 3-32 characters of A-Z, 0-9 or '-'."));
        }

        if (quantity is < 1 or > MaxQuantityPerLine)
        {
            return Result.Failure(DomainError.Validation(
                "order.line_quantity_invalid", $"Quantity must be between 1 and {MaxQuantityPerLine}."));
        }

        if (unitPrice <= 0m || unitPrice > MaxUnitPrice || decimal.Round(unitPrice, 2) != unitPrice)
        {
            return Result.Failure(DomainError.Validation(
                "order.line_price_invalid",
                $"Unit price must be greater than 0, at most {MaxUnitPrice}, with at most two decimals."));
        }

        var existing = _lines.Find(l => l.Sku == normalisedSku);
        if (existing is not null)
        {
            if (existing.UnitPrice != unitPrice)
            {
                return Result.Failure(DomainError.Validation(
                    "order.line_price_mismatch",
                    $"SKU {normalisedSku} is already on the order at {existing.UnitPrice}; one SKU has one price per order."));
            }

            if (existing.Quantity + quantity > MaxQuantityPerLine)
            {
                return Result.Failure(DomainError.Validation(
                    "order.line_quantity_invalid",
                    $"Total quantity for {normalisedSku} would exceed {MaxQuantityPerLine}."));
            }

            existing.Increase(quantity);
        }
        else
        {
            if (_lines.Count >= MaxLines)
            {
                return Result.Failure(DomainError.Validation(
                    "order.too_many_lines", $"An order can have at most {MaxLines} lines."));
            }

            _lines.Add(new OrderLine(normalisedSku, quantity, unitPrice));
        }

        UpdatedAt = Now(clock);
        return Result.Success();
    }

    public Result Submit(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Status != OrderStatus.Draft)
        {
            return NotDraft("Only a draft order can be submitted.");
        }

        if (_lines.Count == 0)
        {
            return Result.Failure(DomainError.Conflict("order.empty", "An order without lines cannot be submitted."));
        }

        Status = OrderStatus.Submitted;
        SubmittedAt = UpdatedAt = Now(clock);
        return Result.Success();
    }

    public Result Cancel(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Status == OrderStatus.Cancelled)
        {
            return Result.Failure(DomainError.Conflict("order.already_cancelled", "The order is already cancelled."));
        }

        Status = OrderStatus.Cancelled;
        CancelledAt = UpdatedAt = Now(clock);
        return Result.Success();
    }

    private Result NotDraft(string message) =>
        Result.Failure(DomainError.Conflict("order.not_draft", $"{message} Current status: {Status}."));

    // PostgreSQL timestamps have microsecond precision. Truncating here means the API returns the same
    // value right after a write as it does after reading the row back.
    private static DateTimeOffset Now(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }

    [GeneratedRegex("^[A-Z0-9-]{3,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();
}
