using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Http.Resilience;
using PartnerIntegrationBFF.Infrastructure.Auth;
using PartnerIntegrationBFF.Infrastructure.Messaging;
using PartnerIntegrationBFF.Shared.Contracts;
using PartnerIntegrationBFF.Shared.Extensions;
using PartnerIntegrationBFF.Shared.Middleware;
using Polly;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddRefitClient<IPartnerClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.Configuration["ExternalApi:BaseUrl"]!))
    .AddResilienceHandler("partner-client", pipeline => {
        pipeline.AddRetry(new HttpRetryStrategyOptions {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential
        });
        pipeline.AddTimeout(TimeSpan.FromSeconds(30));
    });

builder.Services.AddTransient<GlobalExceptionHandlerMiddleware>();

builder.Services.AddSingleton<RabbitMqMessageQueueService>();
builder.Services.AddSingleton<IMessageQueueService>(sp => sp.GetRequiredService<RabbitMqMessageQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqMessageQueueService>());

builder.Services.AddAuthentication("Hmac")
    .AddScheme<AuthenticationSchemeOptions, HmacAuthenticationHandler>("Hmac", null);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints(typeof(Program).Assembly);

app.Run();
