namespace PlantEncyclopedia.Infrastructure.ExternalData;

/// <summary>
/// App-wide cap on calls to the external provider (ARCHITECTURE §23): by default 30 per minute and 1,000 per
/// day, whoever asks. It protects the provider and bounds how many plants imports can create. Fixed windows,
/// in memory: each API instance has its own budget until a shared rate-limit store exists.
/// </summary>
internal class ExternalCallBudget(TimeProvider timeProvider, int perMinute = ExternalCallBudget.PerMinute, int perDay = ExternalCallBudget.PerDay)
{
    /// <summary>The lookup budget's limits.</summary>
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

            if (_minute.Count >= perMinute || _day.Count >= perDay) return false;

            _minute = _minute with { Count = _minute.Count + 1 };
            _day = _day with { Count = _day.Count + 1 };
            return true;
        }
    }

    private readonly record struct Window(DateTimeOffset Start, int Count);
}

/// <summary>
/// A separate app-wide cap for image import (ARCHITECTURE §23): 40 calls per minute and 2,000 per day, counting
/// every iNaturalist and Wikimedia request and image download. Separate so that filling in images never uses up
/// the budget of user-triggered lookups. iNaturalist asks for at most 60 requests per minute.
/// </summary>
internal sealed class ImageCallBudget(TimeProvider timeProvider) : ExternalCallBudget(timeProvider, ImageCallBudget.PerMinute, ImageCallBudget.PerDay)
{
    public new const int PerMinute = 40;
    public new const int PerDay = 2_000;
}

/// <summary>Thrown when <see cref="ImageCallBudget"/> is used up; the image worker waits and tries again.</summary>
internal sealed class ImageCallBudgetExhaustedException() : Exception("The app-wide image import budget is used up.");

/// <summary>Takes one call from <see cref="ImageCallBudget"/> before each request, or throws when it is used up.</summary>
internal sealed class ImageCallBudgetHandler(ImageCallBudget budget) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        budget.TryAcquire() ? base.SendAsync(request, cancellationToken) : throw new ImageCallBudgetExhaustedException();
}
