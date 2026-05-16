using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerIntegrationBFF.Interfaces;
using PartnerIntegrationBFF.Models;

namespace PartnerIntegrationBFF.Controllers;

[ApiController]
[Route("api/v1/partner")]
[Authorize(AuthenticationSchemes = "Hmac")]
public class PartnerController(
    IValidator<PartnerTransactionRequest> validator,
    IPartnerService partnerService)
    : ControllerBase {
    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] PartnerTransactionRequest request) {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) {
            return BadRequest(new {
                success = false,
                message = "Validation failed",
                errors = validation.Errors.Select(e => e.ErrorMessage)
            });
        }

        var isSuccess = await partnerService.ProcessTransactionAsync(request);
        if (!isSuccess)
            return StatusCode(502, new { success = false, message = "Partner verification failed" });

        return Accepted(new { success = true, message = "Transaction accepted and queued" });
    }
}
