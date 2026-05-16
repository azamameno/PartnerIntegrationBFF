using System.Security.Cryptography;
using System.Text;
using PartnerIntegrationBFF.Shared.Extensions;

namespace PartnerIntegrationBFF.Features.Support.GenerateSignature;

public class GenerateSignatureEndpoint : IEndpoint {
    public void Map(IEndpointRouteBuilder app) {
        app.MapPost("/api/v1/support/generate-signature", Handle);
    }

    public static IResult Handle(GenerateSignatureRequest request) {
        if (string.IsNullOrWhiteSpace(request.Secret))
            return Results.BadRequest(new { success = false, message = "secret is required" });
        if (string.IsNullOrWhiteSpace(request.Timestamp))
            return Results.BadRequest(new { success = false, message = "timestamp is required" });
        if (string.IsNullOrWhiteSpace(request.Body))
            return Results.BadRequest(new { success = false, message = "body is required" });

        var bodyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Body))
        ).ToLowerInvariant();

        var signedString = $"{request.Timestamp.Trim()}:{bodyHash}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(request.Secret));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedString)));

        return Results.Ok(new { success = true, bodyHash, signedString, signature });
    }

    public record GenerateSignatureRequest(string Secret, string Timestamp, string Body);
}
