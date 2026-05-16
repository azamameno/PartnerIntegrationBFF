using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Http.Resilience;
using PartnerIntegrationBFF.Infrastructure;
using PartnerIntegrationBFF.Interfaces;
using PartnerIntegrationBFF.Middleware;
using PartnerIntegrationBFF.Services;
using PartnerIntegrationBFF.Validators;
using Polly;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddValidatorsFromAssemblyContaining<PartnerTransactionValidator>();
builder.Services.AddControllers();
builder.Services.AddTransient<GlobalExceptionHandlerMiddleware>();

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

builder.Services.AddScoped<IPartnerService, PartnerService>();

builder.Services.AddSingleton<RabbitMqMessageQueueService>();
builder.Services.AddSingleton<IMessageQueueService>(sp => sp.GetRequiredService<RabbitMqMessageQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqMessageQueueService>());

builder.Services.AddAuthentication("Hmac").AddScheme<AuthenticationSchemeOptions, HmacAuthenticationHandler>("Hmac", null);
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
