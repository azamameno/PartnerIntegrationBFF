using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PartnerIntegrationBFF.Controllers;
using PartnerIntegrationBFF.Interfaces;
using PartnerIntegrationBFF.Models;

namespace PartnerIntegrationBFF.Tests;

public class PartnerControllerTests {
    private readonly Mock<IValidator<PartnerTransactionRequest>> _mockValidator = new();
    private readonly Mock<IPartnerService> _mockPartnerService = new();
    private readonly PartnerController _controller;

    public PartnerControllerTests() {
        this._controller = new PartnerController(this._mockValidator.Object, this._mockPartnerService.Object);
    }

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
    public async Task CreateTransaction_ValidationFails_Returns400() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<PartnerTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Amount", "Invalid") }));

        var result = await this._controller.CreateTransaction(ValidRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateTransaction_ServiceReturnsFalse_Returns502() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<PartnerTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        this._mockPartnerService
            .Setup(s => s.ProcessTransactionAsync(It.IsAny<PartnerTransactionRequest>()))
            .ReturnsAsync(false);

        var result = await this._controller.CreateTransaction(ValidRequest());

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_ServiceReturnsTrue_Returns202() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<PartnerTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        this._mockPartnerService
            .Setup(s => s.ProcessTransactionAsync(It.IsAny<PartnerTransactionRequest>()))
            .ReturnsAsync(true);

        var result = await this._controller.CreateTransaction(ValidRequest());

        Assert.IsType<AcceptedResult>(result);
    }
}
