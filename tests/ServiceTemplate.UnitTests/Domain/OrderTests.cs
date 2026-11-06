using ServiceTemplate.Api.Domain;

namespace ServiceTemplate.UnitTests.Domain;

public sealed class OrderTests
{
    private readonly FixedClock _clock = new();

    [Fact]
    public void Create_trims_reference_and_starts_as_draft()
    {
        var result = Order.Create("  TABLE-7  ", _clock);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerReference.Should().Be("TABLE-7");
        result.Value.Status.Should().Be(OrderStatus.Draft);
        result.Value.CreatedAt.Should().Be(FixedClock.Start);
        result.Value.Lines.Should().BeEmpty();
        result.Value.Total.Should().Be(0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_blank_reference(string? reference)
    {
        var result = Order.Create(reference!, _clock);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("order.customer_reference_invalid");
        result.Error.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public void Create_rejects_reference_longer_than_limit()
    {
        var result = Order.Create(new string('x', Order.MaxCustomerReferenceLength + 1), _clock);

        result.Error!.Code.Should().Be("order.customer_reference_invalid");
    }

    [Fact]
    public void Create_generates_time_ordered_ids()
    {
        var first = Order.Create("A", _clock).Value;
        _clock.Advance(TimeSpan.FromMilliseconds(5));
        var second = Order.Create("B", _clock).Value;

        first.Id.Version.Should().Be(7);
        string.CompareOrdinal(first.Id.ToString(), second.Id.ToString()).Should().BeNegative();
    }

    [Fact]
    public void Timestamps_are_truncated_to_database_precision()
    {
        var clock = new FixedClock(FixedClock.Start.AddTicks(1_234_567));

        var order = Order.Create("A", clock).Value;

        order.CreatedAt.Should().Be(FixedClock.Start.AddTicks(1_234_560), "PostgreSQL stores microseconds");
    }

    [Fact]
    public void AddLine_normalises_sku_and_computes_total()
    {
        var order = NewOrder();

        order.AddLine(" ab-123 ", 3, 12.50m, _clock).IsSuccess.Should().BeTrue();
        order.AddLine("XY-9", 1, 0.99m, _clock).IsSuccess.Should().BeTrue();

        order.Lines.Select(l => l.Sku).Should().Equal("AB-123", "XY-9");
        order.Total.Should().Be(38.49m);
    }

    [Fact]
    public void AddLine_with_same_sku_and_price_merges_quantity()
    {
        var order = NewOrder();

        order.AddLine("AB-123", 2, 5m, _clock);
        order.AddLine("ab-123", 3, 5m, _clock);

        order.Lines.Should().ContainSingle().Which.Quantity.Should().Be(5);
    }

    [Fact]
    public void AddLine_with_same_sku_but_different_price_is_rejected()
    {
        var order = NewOrder();
        order.AddLine("AB-123", 2, 5m, _clock);

        var result = order.AddLine("AB-123", 1, 6m, _clock);

        result.Error!.Code.Should().Be("order.line_price_mismatch");
        order.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2);
    }

    [Fact]
    public void AddLine_rejects_merge_beyond_quantity_limit()
    {
        var order = NewOrder();
        order.AddLine("AB-123", Order.MaxQuantityPerLine, 1m, _clock);

        order.AddLine("AB-123", 1, 1m, _clock).Error!.Code.Should().Be("order.line_quantity_invalid");
    }

    [Theory]
    [InlineData("AB", 1, 1.00, "order.line_sku_invalid")]
    [InlineData("AB_12", 1, 1.00, "order.line_sku_invalid")]
    [InlineData("AB-1", 0, 1.00, "order.line_quantity_invalid")]
    [InlineData("AB-1", 1001, 1.00, "order.line_quantity_invalid")]
    [InlineData("AB-1", 1, 0.00, "order.line_price_invalid")]
    [InlineData("AB-1", 1, -1.00, "order.line_price_invalid")]
    [InlineData("AB-1", 1, 1.005, "order.line_price_invalid")]
    [InlineData("AB-1", 1, 1000000.01, "order.line_price_invalid")]
    public void AddLine_validates_input(string sku, int quantity, double unitPrice, string expectedCode)
    {
        var result = NewOrder().AddLine(sku, quantity, (decimal)unitPrice, _clock);

        result.Error!.Code.Should().Be(expectedCode);
        result.Error.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public void AddLine_rejects_more_than_max_distinct_lines()
    {
        var order = NewOrder();
        for (var i = 0; i < Order.MaxLines; i++)
        {
            order.AddLine($"SKU-{i:D3}", 1, 1m, _clock).IsSuccess.Should().BeTrue();
        }

        order.AddLine("SKU-OVER", 1, 1m, _clock).Error!.Code.Should().Be("order.too_many_lines");
        order.AddLine("SKU-000", 1, 1m, _clock).IsSuccess.Should().BeTrue("merging into an existing line adds no line");
    }

    [Fact]
    public void AddLine_updates_the_modification_timestamp()
    {
        var order = NewOrder();
        _clock.Advance(TimeSpan.FromMinutes(1));

        order.AddLine("AB-1", 1, 1m, _clock);

        order.UpdatedAt.Should().Be(FixedClock.Start.AddMinutes(1));
    }

    [Fact]
    public void Submit_moves_a_non_empty_draft_to_submitted()
    {
        var order = NewOrder();
        order.AddLine("AB-1", 1, 1m, _clock);
        _clock.Advance(TimeSpan.FromMinutes(2));

        order.Submit(_clock).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(OrderStatus.Submitted);
        order.SubmittedAt.Should().Be(FixedClock.Start.AddMinutes(2));
    }

    [Fact]
    public void Submit_rejects_an_empty_order()
    {
        var result = NewOrder().Submit(_clock);

        result.Error!.Code.Should().Be("order.empty");
        result.Error.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Submitted_order_is_frozen()
    {
        var order = NewOrder();
        order.AddLine("AB-1", 1, 1m, _clock);
        order.Submit(_clock);

        order.AddLine("AB-2", 1, 1m, _clock).Error!.Code.Should().Be("order.not_draft");
        order.Submit(_clock).Error!.Code.Should().Be("order.not_draft");
        order.Lines.Should().ContainSingle();
    }

    [Fact]
    public void Cancel_is_allowed_from_draft_and_submitted_but_only_once()
    {
        var draft = NewOrder();
        var submitted = NewOrder();
        submitted.AddLine("AB-1", 1, 1m, _clock);
        submitted.Submit(_clock);

        draft.Cancel(_clock).IsSuccess.Should().BeTrue();
        submitted.Cancel(_clock).IsSuccess.Should().BeTrue();

        draft.Status.Should().Be(OrderStatus.Cancelled);
        draft.CancelledAt.Should().Be(FixedClock.Start);
        draft.Cancel(_clock).Error!.Code.Should().Be("order.already_cancelled");
        draft.Submit(_clock).Error!.Code.Should().Be("order.not_draft");
    }

    [Fact]
    public void Reading_the_value_of_a_failed_result_is_a_programming_error()
    {
        var failed = Order.Create("", _clock);

        var read = () => failed.Value;

        read.Should().Throw<InvalidOperationException>().WithMessage("*order.customer_reference_invalid*");
    }

    private Order NewOrder() => Order.Create("TABLE-1", _clock).Value;
}
