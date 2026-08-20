using KaguERP.Api.Audit;
using KaguERP.Api.Observability;
using KaguERP.Api.Security;
using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Observability;
using KaguERP.BuildingBlocks.Application.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});

builder.Services.AddKaguErpBootstrap(builder.Configuration);
builder.Services.AddKaguErpAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationContextAccessor, HttpCorrelationContextAccessor>();
builder.Services.AddScoped<IExecutionScopeAccessor, HttpExecutionScopeAccessor>();
builder.Services.AddScoped<IRequestAuditContextAccessor, HttpRequestAuditContextAccessor>();

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestTelemetryMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ApplicationScopeMiddleware>();
app.UseAuthorization();

app.MapKaguErpHealthEndpoints();
app.MapExecutionScopeEndpoint();

app.Run();
