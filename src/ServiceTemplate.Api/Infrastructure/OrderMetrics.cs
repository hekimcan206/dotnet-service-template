using System.Diagnostics.Metrics;

namespace ServiceTemplate.Api.Infrastructure;

/// <summary>Business metrics, exported through OpenTelemetry next to the HTTP and runtime metrics.</summary>
public sealed class OrderMetrics
{
    public const string MeterName = "ServiceTemplate.Orders";

    private readonly Counter<long> _created;
    private readonly Counter<long> _submitted;
    private readonly Histogram<double> _submittedValue;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>("orders.created", unit: "{order}", description: "Orders created.");
        _submitted = meter.CreateCounter<long>("orders.submitted", unit: "{order}", description: "Orders submitted.");
        _submittedValue = meter.CreateHistogram<double>(
            "orders.submitted.value", unit: "{currency}", description: "Total value of submitted orders.");
    }

    public void OrderCreated() => _created.Add(1);

    public void OrderSubmitted(decimal total)
    {
        _submitted.Add(1);
        _submittedValue.Record((double)total);
    }
}
