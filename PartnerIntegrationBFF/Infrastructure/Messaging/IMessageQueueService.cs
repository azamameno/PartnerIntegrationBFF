namespace PartnerIntegrationBFF.Infrastructure.Messaging;

public interface IMessageQueueService {
    Task PublishAsync<T>(T message);
}
