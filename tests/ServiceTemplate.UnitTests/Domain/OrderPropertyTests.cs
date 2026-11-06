using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using ServiceTemplate.Api.Domain;

namespace ServiceTemplate.UnitTests.Domain;

/// <summary>
/// Invariants that must hold for any sequence of additions, not just the hand-picked examples above.
/// </summary>
public sealed class OrderPropertyTests
{
    private static readonly string[] Skus = ["AB-1", "AB-2", "CD-3", "EF-4", "GH-5"];

    // Small SKU pool so merges happen often; prices in cents so they are valid two-decimal values.
    private static Gen<(string Sku, int Quantity, decimal UnitPrice)> LineGen =>
        from sku in Gen.Elements(Skus)
        from quantity in Gen.Choose(1, 20)
        from cents in Gen.Choose(1, 100_000)
        select (sku, quantity, cents / 100m);

    public static Arbitrary<(string Sku, int Quantity, decimal UnitPrice)[]> Additions() =>
        Gen.ArrayOf(LineGen).ToArbitrary();

    [Property(Arbitrary = [typeof(OrderPropertyTests)], MaxTest = 300)]
    public bool Total_equals_sum_of_accepted_additions((string Sku, int Quantity, decimal UnitPrice)[] additions)
    {
        var clock = new FixedClock();
        var order = Order.Create("PROP", clock).Value;
        var expected = 0m;

        foreach (var (sku, quantity, unitPrice) in additions)
        {
            if (order.AddLine(sku, quantity, unitPrice, clock).IsSuccess)
            {
                expected += quantity * unitPrice;
            }
        }

        return order.Total == expected;
    }

    [Property(Arbitrary = [typeof(OrderPropertyTests)], MaxTest = 300)]
    public bool Each_sku_appears_once_with_a_single_price((string Sku, int Quantity, decimal UnitPrice)[] additions)
    {
        var clock = new FixedClock();
        var order = Order.Create("PROP", clock).Value;
        var firstPrice = new Dictionary<string, decimal>();

        foreach (var (sku, quantity, unitPrice) in additions)
        {
            if (order.AddLine(sku, quantity, unitPrice, clock).IsSuccess)
            {
                firstPrice.TryAdd(sku, unitPrice);
            }
        }

        return order.Lines.Select(l => l.Sku).Distinct().Count() == order.Lines.Count
            && order.Lines.All(l => l.UnitPrice == firstPrice[l.Sku])
            && order.Lines.All(l => l.Quantity is >= 1 and <= Order.MaxQuantityPerLine);
    }
}
