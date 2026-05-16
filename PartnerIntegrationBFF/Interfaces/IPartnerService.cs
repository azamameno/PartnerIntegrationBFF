using PartnerIntegrationBFF.Models;

namespace PartnerIntegrationBFF.Interfaces;

public interface IPartnerService {
    Task<bool> ProcessTransactionAsync(PartnerTransactionRequest request);
}
