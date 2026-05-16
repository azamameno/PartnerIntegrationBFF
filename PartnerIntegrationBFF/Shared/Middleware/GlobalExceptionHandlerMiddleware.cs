using System.Text.Json;
using FluentValidation;

namespace PartnerIntegrationBFF.Shared.Middleware;

public class GlobalExceptionHandlerMiddleware(ILogger<GlobalExceptionHandlerMiddleware> logger) : IMiddleware {
    public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
        try {
            await next(context);
        } catch (Exception ex) {
            logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex) {
        var (statusCode, message, errors) = ex switch {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                ve.Errors.Select(e => e.ErrorMessage).ToArray()
            ),
            TimeoutException => (
                StatusCodes.Status504GatewayTimeout,
                "Request timed out",
                Array.Empty<string>()
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                Array.Empty<string>()
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                Array.Empty<string>()
            )
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new { success = false, statusCode, message, errors };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
