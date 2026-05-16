using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace PartnerIntegrationBFF.Controllers;

[ApiController]
[Route("api/v1/support")]
public class SupportController : ControllerBase {
    /// <summary>Generate HMAC-SHA256 signature matching the partner auth scheme.</summary>
    /// <remarks>
    /// Signed string format: {timestamp}:{sha256hex(body)}
    /// Signature: Base64(HMAC-SHA256(secret, signedString))
    /// </remarks>
    [HttpPost("generate-signature")]
    public IActionResult GenerateSignature([FromBody] GenerateSignatureRequest request) {
        if (string.IsNullOrWhiteSpace(request.Secret))
            return BadRequest(new { success = false, message = "secret is required" });
        if (string.IsNullOrWhiteSpace(request.Timestamp))
            return BadRequest(new { success = false, message = "timestamp is required" });
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { success = false, message = "body is required" });

        var bodyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Body))
        ).ToLowerInvariant();

        var signedString = $"{request.Timestamp.Trim()}:{bodyHash}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(request.Secret));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedString)));

        return Ok(new {
            success = true,
            bodyHash,
            signedString,
            signature
        });
    }

    public record GenerateSignatureRequest(string Secret, string Timestamp, string Body);
}
