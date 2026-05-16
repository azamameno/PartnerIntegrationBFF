using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace PartnerIntegrationBFF.Infrastructure.Messaging;

public class RabbitMqMessageQueueService(
    IConfiguration configuration,
    ILogger<RabbitMqMessageQueueService> logger)
    : IMessageQueueService, IHostedService {
    private IConnection? _connection;

    public async Task StartAsync(CancellationToken cancellationToken) {
        var factory = new ConnectionFactory {
            HostName = configuration["RabbitMQ:Host"]!,
            Port = int.Parse(configuration["RabbitMQ:Port"]!),
            UserName = configuration["RabbitMQ:Username"]!,
            Password = configuration["RabbitMQ:Password"]!
        };
        this._connection = await factory.CreateConnectionAsync(cancellationToken);
        logger.LogInformation("RabbitMQ connection established");
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        if (this._connection != null)
            await this._connection.CloseAsync(cancellationToken);
    }

    public async Task PublishAsync<T>(T message) {
        var queueName = configuration["RabbitMQ:QueueName"]!;
        await using var channel = await this._connection!.CreateChannelAsync();
        await channel.QueueDeclareAsync(queueName, true, false, false);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await channel.BasicPublishAsync("", queueName, body);
    }
}
