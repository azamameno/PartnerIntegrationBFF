using FluentValidation;
using PartnerIntegrationBFF.Infrastructure.Messaging;
using PartnerIntegrationBFF.Shared.Contracts;
using PartnerIntegrationBFF.Shared.Extensions;

namespace PartnerIntegrationBFF.Features.Partners.CreateTransaction;

public class CreateTransactionEndpoint : IEndpoint {
    public void Map(IEndpointRouteBuilder app) {
        app.MapPost("/api/v1/partner/transactions", Handle)
           .RequireAuthorization();
    }

    public static async Task<IResult> Handle(
        CreateTransactionRequest request,
        IValidator<CreateTransactionRequest> validator,
        IPartnerClient partnerClient,
        IMessageQueueService messageQueueService) {

        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) {
            return Results.BadRequest(new {
                success = false,
                message = "Validation failed",
                errors = validation.Errors.Select(e => e.ErrorMessage)
            });
        }

        var isVerified = await partnerClient.VerifyPartnerAsync(request.PartnerId);
        if (!isVerified)
            return Results.Json(
                new { success = false, message = "Partner verification failed" },
                statusCode: 502);

        await messageQueueService.PublishAsync(request);
        return Results.Accepted(null, new { success = true, message = "Transaction accepted and queued" });
    }
}
