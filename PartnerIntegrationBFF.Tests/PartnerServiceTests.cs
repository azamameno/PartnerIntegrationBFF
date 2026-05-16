using Moq;
using PartnerIntegrationBFF.Interfaces;
using PartnerIntegrationBFF.Models;
using PartnerIntegrationBFF.Services;

namespace PartnerIntegrationBFF.Tests;

public class PartnerServiceTests {
    private readonly Mock<IPartnerClient> _mockPartnerClient = new();
    private readonly Mock<IMessageQueueService> _mockQueueService = new();
    private readonly PartnerService _service;

    public PartnerServiceTests() {
        this._service = new PartnerService(this._mockPartnerClient.Object, this._mockQueueService.Object);
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
    public async Task ProcessTransaction_PartnerNotVerified_ReturnsFalse() {
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await this._service.ProcessTransactionAsync(ValidRequest());

        Assert.False(result);
        this._mockQueueService.Verify(q => q.PublishTransactionAsync(It.IsAny<PartnerTransactionRequest>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTransaction_PartnerClientThrows_PropagatesException() {
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        await Assert.ThrowsAsync<HttpRequestException>(() => this._service.ProcessTransactionAsync(ValidRequest()));
    }

    [Fact]
    public async Task ProcessTransaction_AllOk_ReturnsTrue() {
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        this._mockQueueService
            .Setup(q => q.PublishTransactionAsync(It.IsAny<PartnerTransactionRequest>()))
            .Returns(Task.CompletedTask);

        var result = await this._service.ProcessTransactionAsync(ValidRequest());

        Assert.True(result);
    }

    [Fact]
    public async Task ProcessTransaction_QueueThrows_PropagatesException() {
        this._mockPartnerClient
            .Setup(c => c.VerifyPartnerAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        this._mockQueueService
            .Setup(q => q.PublishTransactionAsync(It.IsAny<PartnerTransactionRequest>()))
            .ThrowsAsync(new Exception("Queue down"));

        await Assert.ThrowsAsync<Exception>(() => this._service.ProcessTransactionAsync(ValidRequest()));
    }
}
