var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/v1/partner/{partnerId}/verify", (string partnerId) =>
    Results.Ok(Random.Shared.NextDouble() >= 0.3));

app.Run();
