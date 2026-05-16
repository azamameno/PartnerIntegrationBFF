using PartnerIntegrationBFF.Interfaces;
using PartnerIntegrationBFF.Models;

namespace PartnerIntegrationBFF.Services;

public class PartnerService(IPartnerClient partnerClient, IMessageQueueService messageQueueService)
    : IPartnerService
{
    public async Task<bool> ProcessTransactionAsync(PartnerTransactionRequest request)
    {
        var isVerified = await partnerClient.VerifyPartnerAsync(request.PartnerId);
        if (!isVerified) {
            return false;
        }
        await messageQueueService.PublishTransactionAsync(request);
        return true;
    }
}
