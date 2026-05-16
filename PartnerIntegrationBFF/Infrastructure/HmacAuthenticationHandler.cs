using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PartnerIntegrationBFF.Infrastructure;

public class HmacAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    private const string TimestampHeader = "X-Timestamp";
    private const string SignatureHeader = "X-Signature";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
        if (!this.Request.Headers.TryGetValue(SignatureHeader, out var providedSignature))
            return AuthenticateResult.NoResult();

        if (!this.Request.Headers.TryGetValue(TimestampHeader, out var timestamp))
            return AuthenticateResult.Fail("Missing X-Timestamp");

        this.Request.EnableBuffering();
        using var reader = new StreamReader(this.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        this.Request.Body.Position = 0;

        string? partnerId;
        try {
            using var doc = JsonDocument.Parse(body);
            partnerId = doc.RootElement.TryGetProperty("partnerId", out var el)
                ? el.GetString() : null;
        }
        catch { return AuthenticateResult.Fail("Invalid request body"); }

        if (string.IsNullOrEmpty(partnerId))
            return AuthenticateResult.Fail("Missing partnerId");

        var partnerSecret = configuration[$"Security:PartnerSecrets:{partnerId}"];
        if (string.IsNullOrEmpty(partnerSecret))
            return AuthenticateResult.Fail("Unknown partner");

        var bodyHash = ComputeSha256Hex(body);
        var signedString = $"{timestamp.ToString().Trim()}:{bodyHash}";
        var expectedSignature = ComputeHmacSha256Base64(partnerSecret, signedString);

        // Timing-safe HMAC comparison
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedSignature.ToString().Trim()),
            Encoding.UTF8.GetBytes(expectedSignature)))
            return AuthenticateResult.Fail("Invalid signature");

        var claims = new[] { new Claim("PartnerId", partnerId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, this.Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, this.Scheme.Name));
    }

    private static string ComputeSha256Hex(string input) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeHmacSha256Base64(string secret, string data) {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }
}
