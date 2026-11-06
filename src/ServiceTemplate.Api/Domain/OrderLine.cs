namespace ServiceTemplate.Api.Domain;

/// <summary>A product line on an order. Only <see cref="Order"/> can create or change lines.</summary>
public sealed class OrderLine
{
    internal OrderLine(string sku, int quantity, decimal unitPrice)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string Sku { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    /// <summary>Exact: unit prices carry at most two decimals and quantities are integers, so no rounding is needed.</summary>
    public decimal LineTotal => Quantity * UnitPrice;

    internal void Increase(int quantity) => Quantity += quantity;
}
