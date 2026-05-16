using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PartnerIntegrationBFF.Infrastructure;

namespace PartnerIntegrationBFF.Tests.Infrastructure;

public class HmacAuthHandlerSignatureTests {
    private const string ValidPartnerId = "partner-01";
    private const string ValidSecret = "test-secret-abc123";
    private const string ValidTimestamp = "1747382400";

    private static readonly string ValidBody =
        $$$"""{"partnerId":"{{{ValidPartnerId}}}","transactionReference":"ref-001","amount":100,"currency":"USD","timestamp":"2026-01-01T00:00:00Z"}""";

    private static HmacAuthenticationHandler BuildHandler(Dictionary<string, string?> secrets) {
        var dict = secrets.ToDictionary(
            kv => $"Security:PartnerSecrets:{kv.Key}",
            kv => kv.Value);
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
        return new HmacAuthenticationHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, config);
    }

    private static HttpContext BuildContext(string body, string? timestamp, string? signature) {
        var ctx = new DefaultHttpContext();
        if (timestamp != null) ctx.Request.Headers["X-Timestamp"] = timestamp;
        if (signature != null) ctx.Request.Headers["X-Signature"] = signature;
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;
        return ctx;
    }

    private static string ComputeSignature(string secret, string body, string timestamp) {
        var bodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}:{bodyHash}")));
    }

    private static async Task<AuthenticateResult> Run(HmacAuthenticationHandler handler, HttpContext ctx) {
        var scheme = new AuthenticationScheme("Hmac", null, typeof(HmacAuthenticationHandler));
        await handler.InitializeAsync(scheme, ctx);
        return await handler.AuthenticateAsync();
    }

    // --- Happy path ---

    [Fact]
    public async Task ValidRequest_ReturnsSuccess_WithPartnerIdClaim() {
        var sig = ComputeSignature(ValidSecret, ValidBody, ValidTimestamp);
        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext(ValidBody, ValidTimestamp, sig));

        Assert.True(result.Succeeded);
        Assert.Equal(ValidPartnerId, result.Principal?.FindFirst("PartnerId")?.Value);
    }

    // --- Header validation ---

    [Fact]
    public async Task MissingSignature_ReturnsNoResult() {
        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext(ValidBody, ValidTimestamp, signature: null));

        Assert.True(result.None);
    }

    [Fact]
    public async Task MissingTimestamp_ReturnsFail() {
        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext(ValidBody, timestamp: null, signature: "sig"));

        Assert.False(result.Succeeded);
        Assert.Contains("X-Timestamp", result.Failure!.Message);
    }

    // --- Body validation ---

    [Fact]
    public async Task InvalidJsonBody_ReturnsFail() {
        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext("not-valid-json", ValidTimestamp, "sig"));

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid request body", result.Failure!.Message);
    }

    [Fact]
    public async Task BodyMissingPartnerId_ReturnsFail() {
        var body = """{"transactionReference":"ref-001","amount":100,"currency":"USD"}""";
        var sig = ComputeSignature(ValidSecret, body, ValidTimestamp);
        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext(body, ValidTimestamp, sig));

        Assert.False(result.Succeeded);
        Assert.Contains("partnerId", result.Failure!.Message);
    }

    // --- Partner / secret validation ---

    [Fact]
    public async Task UnknownPartner_ReturnsFail() {
        var result = await Run(
            BuildHandler(new()),
            BuildContext(ValidBody, ValidTimestamp, "any-sig"));

        Assert.False(result.Succeeded);
        Assert.Contains("Unknown partner", result.Failure!.Message);
    }

    // --- HMAC integrity ---

    [Fact]
    public async Task WrongSignature_ReturnsFail() {
        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext(ValidBody, ValidTimestamp, "aW52YWxpZC1zaWduYXR1cmU="));

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid signature", result.Failure!.Message);
    }

    [Fact]
    public async Task TamperedBody_ReturnsFail() {
        // Sign original body, but send modified body
        var originalBody = ValidBody;
        var tamperedBody = ValidBody.Replace("100", "99999");
        var sig = ComputeSignature(ValidSecret, originalBody, ValidTimestamp);

        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext(tamperedBody, ValidTimestamp, sig));

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid signature", result.Failure!.Message);
    }

    [Fact]
    public async Task WrongSecret_ReturnsFail() {
        var sig = ComputeSignature("wrong-secret", ValidBody, ValidTimestamp);
        var result = await Run(
            BuildHandler(new() { [ValidPartnerId] = ValidSecret }),
            BuildContext(ValidBody, ValidTimestamp, sig));

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid signature", result.Failure!.Message);
    }
}
