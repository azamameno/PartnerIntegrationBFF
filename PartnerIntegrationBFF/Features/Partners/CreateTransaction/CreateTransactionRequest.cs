namespace PartnerIntegrationBFF.Features.Partners.CreateTransaction;

public class CreateTransactionRequest {
    public string PartnerId { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
