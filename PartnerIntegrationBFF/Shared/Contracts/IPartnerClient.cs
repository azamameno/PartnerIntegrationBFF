using Refit;

namespace PartnerIntegrationBFF.Shared.Contracts;

public interface IPartnerClient {
    [Get("/api/v1/partner/{partnerId}/verify")]
    Task<bool> VerifyPartnerAsync(string partnerId);
}
