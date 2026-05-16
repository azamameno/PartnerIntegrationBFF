using PartnerIntegrationBFF.Models;

namespace PartnerIntegrationBFF.Interfaces;

public interface IMessageQueueService {
    Task PublishTransactionAsync(PartnerTransactionRequest request);
}
