using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Moq;
using PartnerIntegrationBFF.Features.Partners.CreateTransaction;
using PartnerIntegrationBFF.Infrastructure.Messaging;
using PartnerIntegrationBFF.Shared.Contracts;

namespace PartnerIntegrationBFF.Tests;

public class CreateTransactionEndpointTests {
    private readonly Mock<IValidator<CreateTransactionRequest>> _mockValidator = new();
    private readonly Mock<IPartnerClient> _mockPartnerClient = new();
    private readonly Mock<IMessageQueueService> _mockQueueService = new();

    private static CreateTransactionRequest ValidRequest() => new() {
        PartnerId = "partner-01",
        TransactionReference = "ref-001",
        Amount = 250m,
        Currency = "USD",
        Timestamp = DateTime.UtcNow
    };

    private Task<IResult> CallHandle() =>
        CreateTransactionEndpoint.Handle(
            ValidRequest(),
            this._mockValidator.Object,
            this._mockPartnerClient.Object,
            this._mockQueueService.Object);

    [Fact]
    public async Task Handle_ValidationFails_Returns400() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Amount", "Invalid") }));

        var result = await this.CallHandle();

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, status.StatusCode);
    }

    [Fact]
    public async Task Handle_PartnerNotVerified_Returns502() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await this.CallHandle();

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(502, status.StatusCode);
        this._mockQueueService.Verify(q => q.PublishAsync(It.IsAny<CreateTransactionRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PartnerClientThrows_PropagatesException() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        await Assert.ThrowsAsync<HttpRequestException>(() => this.CallHandle());
    }

    [Fact]
    public async Task Handle_QueueThrows_PropagatesException() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        this._mockQueueService
            .Setup(q => q.PublishAsync(It.IsAny<CreateTransactionRequest>()))
            .ThrowsAsync(new Exception("Queue down"));

        await Assert.ThrowsAsync<Exception>(() => this.CallHandle());
    }

    [Fact]
    public async Task Handle_AllOk_Returns202() {
        this._mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        this._mockQueueService
            .Setup(q => q.PublishAsync(It.IsAny<CreateTransactionRequest>()))
            .Returns(Task.CompletedTask);

        var result = await this.CallHandle();

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(202, status.StatusCode);
    }
}
