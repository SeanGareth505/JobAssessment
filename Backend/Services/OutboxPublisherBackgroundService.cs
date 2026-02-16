using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class OutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisherBackgroundService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static string QueueNameForType(string type) => type switch
    {
        "OrderCreated" => "order-created",
        "CustomerCreated" => "customer-created",
        _ => "order-created"
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox publisher started (interval: {Interval}, batch: {Batch}).", PollInterval, BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher iteration failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rabbitMq = scope.ServiceProvider.GetRequiredService<IRabbitMqService>();

        if (!rabbitMq.IsConnected)
        {
            logger.LogDebug("RabbitMQ not connected; skipping outbox poll.");
            return;
        }

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        foreach (var msg in pending)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var queue = QueueNameForType(msg.Type);
                rabbitMq.Publish(queue, msg.Payload);
                msg.ProcessedAtUtc = DateTime.UtcNow;
                logger.LogInformation("Outbox message {Id} (Type={Type}) published to {Queue}.", msg.Id, msg.Type, queue);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish outbox message {Id}; will retry next poll.", msg.Id);
                break;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
