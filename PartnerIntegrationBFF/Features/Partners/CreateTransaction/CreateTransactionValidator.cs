using FluentValidation;

namespace PartnerIntegrationBFF.Features.Partners.CreateTransaction;

public class CreateTransactionValidator : AbstractValidator<CreateTransactionRequest> {
    private static readonly string[] ValidCurrencies = ["USD", "EUR", "GBP", "VND", "JPY", "SGD"];

    public CreateTransactionValidator() {
        RuleFor(x => x.PartnerId).NotEmpty();
        RuleFor(x => x.TransactionReference).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => ValidCurrencies.Contains(c))
            .WithMessage("Currency must be one of: USD, EUR, GBP, VND, JPY, SGD");
        RuleFor(x => x.Timestamp).NotEqual(default(DateTime));
    }
}
