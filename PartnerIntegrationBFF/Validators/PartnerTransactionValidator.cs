using FluentValidation;
using PartnerIntegrationBFF.Models;

namespace PartnerIntegrationBFF.Validators;

public class PartnerTransactionValidator : AbstractValidator<PartnerTransactionRequest> {
    private static readonly string[] ValidCurrencies = ["USD", "EUR", "GBP", "VND", "JPY", "SGD"];

    public PartnerTransactionValidator() {
        this.RuleFor(x => x.PartnerId).NotEmpty();
        this.RuleFor(x => x.TransactionReference).NotEmpty();
        this.RuleFor(x => x.Amount).GreaterThan(0);
        this.RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => ValidCurrencies.Contains(c))
            .WithMessage("Currency must be one of: USD, EUR, GBP, VND, JPY, SGD");
        this.RuleFor(x => x.Timestamp).NotEqual(default(DateTime));
    }
}
