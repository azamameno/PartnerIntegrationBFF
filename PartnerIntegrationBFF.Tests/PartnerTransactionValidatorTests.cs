using FluentValidation.TestHelper;
using PartnerIntegrationBFF.Models;
using PartnerIntegrationBFF.Validators;

namespace PartnerIntegrationBFF.Tests;

public class PartnerTransactionValidatorTests {
    private readonly PartnerTransactionValidator _validator = new();

    private static PartnerTransactionRequest ValidRequest() {
        return new PartnerTransactionRequest {
            PartnerId = "partner-01",
            TransactionReference = "ref-001",
            Amount = 250m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Amount_Zero_ShouldFail() {
        var r = ValidRequest();
        r.Amount = 0;
        this._validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_Negative_ShouldFail() {
        var r = ValidRequest();
        r.Amount = -100;
        this._validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_Valid_ShouldPass() {
        var r = ValidRequest();
        r.Amount = 250;
        this._validator.TestValidate(r).ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Currency_Invalid_ShouldFail() {
        var r = ValidRequest();
        r.Currency = "XYZ";
        this._validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Currency_Valid_ShouldPass() {
        var r = ValidRequest();
        r.Currency = "USD";
        this._validator.TestValidate(r).ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void PartnerId_Empty_ShouldFail() {
        var r = ValidRequest();
        r.PartnerId = "";
        this._validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.PartnerId);
    }

    [Fact]
    public void TransactionReference_Empty_ShouldFail() {
        var r = ValidRequest();
        r.TransactionReference = "";
        this._validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.TransactionReference);
    }

    [Fact]
    public void Timestamp_Default_ShouldFail() {
        var r = ValidRequest();
        r.Timestamp = default;
        this._validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Timestamp);
    }

    [Fact]
    public void AllFieldsValid_ShouldPass() {
        this._validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }
}
