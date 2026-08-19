using KaguERP.Bootstrap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKaguErpBootstrap();

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));

app.Run();

