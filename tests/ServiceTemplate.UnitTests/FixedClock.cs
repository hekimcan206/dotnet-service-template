namespace ServiceTemplate.UnitTests;

/// <summary>Deterministic clock: tests assert exact timestamps instead of "close to now".</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset Start = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    public FixedClock()
        : this(Start)
    {
    }

    public DateTimeOffset Now { get; private set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
