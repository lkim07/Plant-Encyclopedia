using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlantEncyclopedia.Application.ExternalData.Images;

namespace PlantEncyclopedia.Infrastructure.ExternalData.Images;

/// <summary>
/// Plants waiting for image import, in memory. Bounded: when 1,000 plants are already waiting, new ones are
/// dropped (a later backfill picks them up). Lost on restart for the same reason.
/// </summary>
internal sealed class PlantImageQueue : IPlantImageQueue
{
    public const int Capacity = 1_000;

    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public void Enqueue(Guid plantId) => _channel.Writer.TryWrite(plantId);
}

/// <summary>Used when image import is disabled (the default, and always in tests): nothing is queued.</summary>
internal sealed class DisabledPlantImageQueue : IPlantImageQueue
{
    public void Enqueue(Guid plantId)
    {
    }
}

/// <summary>
/// Imports images for queued plants one at a time, in the background (ARCHITECTURE §23), so a lookup returns
/// before any image work starts. When the image budget is used up, the plant goes back in the queue after a
/// pause. Other failures are logged (without the plant name) and the worker moves on.
/// </summary>
internal sealed partial class PlantImageWorker(
    PlantImageQueue queue,
    IServiceScopeFactory scopes,
    PlantImageOptions options,
    TimeProvider timeProvider,
    ILogger<PlantImageWorker> logger) : BackgroundService
{
    public static readonly TimeSpan BudgetPause = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.BackfillOnStartup)
        {
            await QueueBackfillAsync(stoppingToken);
        }

        await foreach (var plantId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(plantId, stoppingToken);
        }
    }

    internal async Task ProcessAsync(Guid plantId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<PlantImageImportService>().ImportAsync(plantId, stoppingToken);
            LogImported(logger, plantId, result.Status, result.Added);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ImageCallBudgetExhaustedException)
        {
            LogBudgetExhausted(logger, ImageCallBudget.PerMinute, ImageCallBudget.PerDay, BudgetPause.TotalSeconds);
            await Task.Delay(BudgetPause, timeProvider, stoppingToken);
            queue.Enqueue(plantId);
        }
        catch (Exception e)
        {
            LogFailed(logger, e, plantId);
        }
    }

    private async Task QueueBackfillAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var plants = await scope.ServiceProvider.GetRequiredService<IPlantImageStore>()
                .FindPlantsWithoutImagesAsync(options.BackfillLimit, stoppingToken);
            foreach (var plantId in plants) queue.Enqueue(plantId);
            LogBackfillQueued(logger, plants.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            LogBackfillFailed(logger, e);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Image import for plant {PlantId}: {Status}, {Added} added.")]
    private static partial void LogImported(ILogger logger, Guid plantId, PlantImageImportStatus status, int added);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Image import paused: the image budget ({PerMinute}/minute, {PerDay}/day) is used up; retrying in {Seconds} s.")]
    private static partial void LogBudgetExhausted(ILogger logger, int perMinute, int perDay, double seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image import for plant {PlantId} failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid plantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Image backfill queued {Count} plants without images.")]
    private static partial void LogBackfillQueued(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image backfill could not list plants.")]
    private static partial void LogBackfillFailed(ILogger logger, Exception exception);
}
