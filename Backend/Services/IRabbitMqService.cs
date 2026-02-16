namespace Backend.Services;

public interface IRabbitMqService
{
    void Publish(string queueName, string message);
    bool IsConnected { get; }
}
