using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Worker;

public class Worker(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<Worker> logger) : BackgroundService
{
    private const string OrderCreatedQueue = "order-created";
    private const string CustomerCreatedQueue = "customer-created";
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private IConnection? _connection;
    private IModel? _channelOrder;
    private IModel? _channelCustomer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await VerifyDatabaseConnectionAsync();

        var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
        var port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true
        };

        await EnsureConnectedAndConsumeAsync(factory, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_connection?.IsOpen != true)
            {
                try
                {
                    _channelOrder?.Dispose();
                    _channelCustomer?.Dispose();
                    _connection?.Dispose();
                    await EnsureConnectedAndConsumeAsync(factory, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Reconnect failed. Retrying in 5s...");
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task VerifyDatabaseConnectionAsync()
    {
        var connStr = configuration.GetConnectionString("DefaultConnection");
        var server = connStr != null && connStr.Contains("Server=")
            ? System.Text.RegularExpressions.Regex.Match(connStr, @"Server=([^;]+)").Groups[1].Value
            : "(not set)";
        logger.LogInformation("Worker: Using database Server={Server} (from ConnectionStrings:DefaultConnection)", server);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await db.Database.CanConnectAsync();
            if (canConnect)
                logger.LogInformation("Worker: Database connection OK.");
            else
                logger.LogWarning("Worker: Database CanConnectAsync returned false.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Worker: Database connection FAILED. Fix DB (SQL Server running? Same server as Backend?) then restart Worker. Messages will not be acked until DB is reachable.");
        }
    }

    private async Task EnsureConnectedAndConsumeAsync(ConnectionFactory factory, CancellationToken stoppingToken)
    {
        _connection = factory.CreateConnection();
        _channelOrder = _connection.CreateModel();
        _channelOrder.QueueDeclare(queue: OrderCreatedQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channelOrder.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var orderConsumer = new AsyncEventingBasicConsumer(_channelOrder);
        orderConsumer.Received += async (sender, ea) =>
        {
            var channel = ((AsyncEventingBasicConsumer)sender!).Model;
            var correlationId = GetCorrelationId(ea);
            using (logger.BeginScope("CorrelationId={CorrelationId}", correlationId))
            {
                await HandleOrderCreatedAsync(channel, ea, correlationId);
            }
        };
        _channelOrder.BasicConsume(queue: OrderCreatedQueue, autoAck: false, consumer: orderConsumer);

        _channelCustomer = _connection.CreateModel();
        _channelCustomer.QueueDeclare(queue: CustomerCreatedQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channelCustomer.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var customerConsumer = new AsyncEventingBasicConsumer(_channelCustomer);
        customerConsumer.Received += async (sender, ea) =>
        {
            logger.LogInformation("[Worker][customer-created] Received message (DeliveryTag={DeliveryTag}). Invoking handler.", ea.DeliveryTag);
            var channel = ((AsyncEventingBasicConsumer)sender!).Model;
            var correlationId = GetCorrelationId(ea);
            using (logger.BeginScope("CorrelationId={CorrelationId}", correlationId))
            {
                await HandleCustomerCreatedAsync(channel, ea, correlationId);
            }
        };
        _channelCustomer.BasicConsume(queue: CustomerCreatedQueue, autoAck: false, consumer: customerConsumer);

        var processId = Environment.ProcessId;
        logger.LogInformation("Worker listening on queues: {OrderQueue}, {CustomerQueue} (ProcessId={ProcessId}). Only ONE Worker should run. If you never see 'Received message', purge the queue in RabbitMQ (Queues > customer-created > Purge Messages) and restart Worker.", OrderCreatedQueue, CustomerCreatedQueue, processId);
        await Task.CompletedTask;
    }

    private static string GetCorrelationId(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties?.Headers != null && ea.BasicProperties.Headers.TryGetValue("X-Correlation-ID", out var raw) && raw is byte[] bytes)
            return Encoding.UTF8.GetString(bytes);
        return Guid.NewGuid().ToString("N");
    }

    private async Task HandleOrderCreatedAsync(IModel channel, BasicDeliverEventArgs ea, string correlationId)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var retryCount = GetRetryCount(ea);

        try
        {
            var payload = JsonSerializer.Deserialize<OrderCreatedPayload>(message);
            if (payload?.Id == null || payload.Id == Guid.Empty)
            {
                logger.LogWarning("[Worker][order-created] Invalid message: missing Order Id. Raw: {Message}", message);
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            await ProcessOrderCreatedAsync(payload);
            channel.BasicAck(ea.DeliveryTag, false);
            logger.LogInformation("[Worker][order-created] Success: Order {OrderId} processed and acked.", payload.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Worker][order-created] Failure (retry {Retry}/{Max})", retryCount + 1, MaxRetries);
            if (retryCount >= MaxRetries - 1)
            {
                channel.BasicNack(ea.DeliveryTag, false, false);
                logger.LogWarning("[Worker][order-created] Message nacked without requeue after {Max} retries; consider DLQ", MaxRetries);
            }
            else
            {
                channel.BasicNack(ea.DeliveryTag, false, true);
                await Task.Delay(RetryDelay);
            }
        }
    }

    private static int GetRetryCount(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties?.Headers == null) return 0;
        if (ea.BasicProperties.Headers.TryGetValue("x-death", out var death) && death is IList<object> list && list.Count > 0)
        {
            if (list[0] is Dictionary<string, object> dict && dict.TryGetValue("count", out var c) && c is long n)
                return (int)n;
        }
        return 0;
    }

    private async Task ProcessOrderCreatedAsync(OrderCreatedPayload payload)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await db.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == payload.Id);

        var autoFulfill = configuration.GetValue("Worker:AutoFulfill", false);
        if (order != null)
        {
            if (order.Status == OrderStatus.Fulfilled || order.Status == OrderStatus.Cancelled)
            {
                logger.LogInformation("[Worker][order-created] Order {OrderId} already {Status} (idempotent skip)", order.Id, order.Status);
                return;
            }
            if (autoFulfill)
            {
                await Task.Delay(300);
                order.Status = OrderStatus.Fulfilled;
                await db.SaveChangesAsync();
                logger.LogInformation("[Worker][order-created] Order {OrderId} set to Fulfilled", order.Id);
            }
            else
            {
                logger.LogInformation("[Worker][order-created] Order {OrderId} left as {Status} (AutoFulfill disabled)", order.Id, order.Status);
            }
            return;
        }

        var orderId = payload.Id!.Value;
        var createdAt = payload.OccurredAtUtc ?? DateTime.UtcNow;
        var newOrder = new Order
        {
            Id = orderId,
            CustomerId = payload.CustomerId ?? Guid.Empty,
            Status = OrderStatus.Pending,
            CreatedAt = createdAt,
            CurrencyCode = payload.CurrencyCode ?? "ZAR",
            TotalAmount = payload.TotalAmount ?? 0
        };

        foreach (var li in payload.LineItems ?? [])
        {
            newOrder.LineItems.Add(new OrderLineItem
            {
                Id = li.Id ?? Guid.NewGuid(),
                OrderId = orderId,
                ProductSku = li.ProductSku ?? string.Empty,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            });
        }

        db.Orders.Add(newOrder);
        await db.SaveChangesAsync();
        logger.LogInformation("[Worker][order-created] Order {OrderId} persisted to DB with {Count} line items.", orderId, newOrder.LineItems.Count);

        if (autoFulfill)
        {
            await Task.Delay(300);
            newOrder.Status = OrderStatus.Fulfilled;
            await db.SaveChangesAsync();
            logger.LogInformation("[Worker][order-created] Order {OrderId} set to Fulfilled", orderId);
        }
    }

    private async Task HandleCustomerCreatedAsync(IModel channel, BasicDeliverEventArgs ea, string correlationId)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var retryCount = GetRetryCount(ea);

        try
        {
            var payload = JsonSerializer.Deserialize<CustomerCreatedPayload>(message);
            if (payload?.Id == null || payload.Id == Guid.Empty)
            {
                logger.LogWarning("[Worker][customer-created] Invalid message: missing Customer Id. Raw: {Message}. Acking to discard.", message);
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            logger.LogInformation("[Worker][customer-created] Processing Customer {CustomerId} {Name} ({CountryCode})", payload.Id, payload.Name, payload.CountryCode);

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            logger.LogInformation("[Worker][customer-created] Checking if Customer {CustomerId} already exists...", payload.Id);
            var exists = await db.Customers.AnyAsync(c => c.Id == payload.Id);
            if (exists)
            {
                logger.LogInformation("[Worker][customer-created] Customer {CustomerId} already exists (idempotent skip).", payload.Id);
            }
            else
            {
                logger.LogInformation("[Worker][customer-created] Inserting Customer {CustomerId} into DB...", payload.Id);
                var customer = new Customer
                {
                    Id = payload.Id.Value,
                    Name = payload.Name ?? string.Empty,
                    Email = payload.Email ?? string.Empty,
                    CountryCode = payload.CountryCode ?? string.Empty,
                    CreatedAt = payload.OccurredAtUtc ?? DateTime.UtcNow
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
                logger.LogInformation("[Worker][customer-created] Customer {CustomerId} persisted to DB.", payload.Id);
            }

            channel.BasicAck(ea.DeliveryTag, false);
            logger.LogInformation("[Worker][customer-created] Success: Customer {CustomerId} processed and acked.", payload.Id);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
            logger.LogError(ex, "[Worker][customer-created] FAILED (retry {Retry}/{Max}): {Message}{Inner}. Fix the error then restart Worker.", retryCount + 1, MaxRetries, ex.Message, inner);
            if (retryCount >= MaxRetries - 1)
            {
                channel.BasicNack(ea.DeliveryTag, false, false);
                logger.LogWarning("[Worker][customer-created] Message nacked without requeue after {Max} retries; consider DLQ", MaxRetries);
            }
            else
            {
                channel.BasicNack(ea.DeliveryTag, false, true);
                await Task.Delay(RetryDelay);
            }
        }
    }

    private sealed record OrderCreatedLineItemPayload(
        [property: JsonPropertyName("id")] Guid? Id,
        [property: JsonPropertyName("orderId")] Guid? OrderId,
        [property: JsonPropertyName("productSku")] string? ProductSku,
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("unitPrice")] decimal UnitPrice);

    private sealed record OrderCreatedPayload(
        [property: JsonPropertyName("id")] Guid? Id,
        [property: JsonPropertyName("customerId")] Guid? CustomerId,
        [property: JsonPropertyName("totalAmount")] decimal? TotalAmount,
        [property: JsonPropertyName("currencyCode")] string? CurrencyCode,
        [property: JsonPropertyName("occurredAtUtc")] DateTime? OccurredAtUtc,
        [property: JsonPropertyName("lineItems")] List<OrderCreatedLineItemPayload>? LineItems);

    private sealed record CustomerCreatedPayload(
        [property: JsonPropertyName("id")] Guid? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("countryCode")] string? CountryCode,
        [property: JsonPropertyName("occurredAtUtc")] DateTime? OccurredAtUtc);

    public override void Dispose()
    {
        _channelOrder?.Dispose();
        _channelCustomer?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
