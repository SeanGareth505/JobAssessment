using System.Text;
using RabbitMQ.Client;

namespace Backend.Services;

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ILogger<RabbitMqService> _logger;
    private readonly string _hostName;
    private readonly int _port;

    public bool IsConnected => _connection?.IsOpen ?? false;

    public RabbitMqService(IConfiguration configuration, ILogger<RabbitMqService> logger)
    {
        _logger = logger;
        _hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
        _port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _hostName,
                Port = _port,
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest"
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _logger.LogInformation("RabbitMQ connected to {Host}:{Port}", _hostName, _port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ not available at {Host}:{Port}. Message publishing will be no-op.", _hostName, _port);
        }
    }

    public void Publish(string queueName, string message)
    {
        if (_channel == null || !_channel.IsOpen)
        {
            _logger.LogWarning("RabbitMQ not connected; cannot publish to {Queue}. Caller should not mark outbox as processed.", queueName);
            throw new InvalidOperationException($"RabbitMQ not connected; cannot publish to {queueName}.");
        }

        try
        {
            var durable = queueName is "order-created" or "customer-created";
            _channel.QueueDeclare(queue: queueName, durable: durable, exclusive: false, autoDelete: false, arguments: null);
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
            _logger.LogInformation("RabbitMQ publish success: queue={Queue}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ publish failed: queue={Queue}", queueName);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
