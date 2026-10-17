namespace PlantEncyclopedia.Infrastructure.ExternalData;

/// <summary>
/// App-wide cap on calls to the external provider (ARCHITECTURE §23): 30 per minute and 1,000 per day,
/// whoever asks. It protects the provider and bounds how many plants imports can create. Fixed windows,
/// in memory: each API instance has its own budget until a shared rate-limit store exists.
/// </summary>
internal sealed class ExternalCallBudget(TimeProvider timeProvider)
{
    public const int PerMinute = 30;
    public const int PerDay = 1_000;

    private readonly Lock _lock = new();
    private Window _minute = new(DateTimeOffset.MinValue, 0);
    private Window _day = new(DateTimeOffset.MinValue, 0);

    /// <summary>Takes one call from both windows, or returns false (taking nothing) when either is used up.</summary>
    public bool TryAcquire()
    {
        var now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (now - _minute.Start >= TimeSpan.FromMinutes(1)) _minute = new Window(now, 0);
            if (now - _day.Start >= TimeSpan.FromDays(1)) _day = new Window(now, 0);

            if (_minute.Count >= PerMinute || _day.Count >= PerDay) return false;

            _minute = _minute with { Count = _minute.Count + 1 };
            _day = _day with { Count = _day.Count + 1 };
            return true;
        }
    }

    private readonly record struct Window(DateTimeOffset Start, int Count);
}
