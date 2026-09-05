using KaguERP.Api.Audit;
using KaguERP.Api.Observability;
using KaguERP.Api.Reports;
using KaguERP.Api.Security;
using KaguERP.Api.Sales;
using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Observability;
using KaguERP.BuildingBlocks.Application.Security;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
bool isOpenApiDocumentGeneration =
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});

builder.Services.AddKaguErpBootstrap(builder.Configuration);
if (!isOpenApiDocumentGeneration)
{
    builder.Services.AddKaguErpAuthentication(builder.Configuration);
}
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Servers = [new Microsoft.OpenApi.OpenApiServer { Url = "/" }];
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes =
            new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>
            {
                ["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
                {
                    Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                },
            };

        foreach (Microsoft.OpenApi.IOpenApiPathItem path in document.Paths.Values)
        {
            if (path.Operations is not { } operations)
            {
                continue;
            }

            foreach (Microsoft.OpenApi.OpenApiOperation operation in operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(new Microsoft.OpenApi.OpenApiSecurityRequirement
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = [],
                });
            }
        }

        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, _, _) =>
    {
        for (int index = 0; index < (operation.Parameters?.Count ?? 0); index++)
        {
            Microsoft.OpenApi.IOpenApiParameter parameter = operation.Parameters![index];
            if (parameter.Name is SalesOrderLifecycleEndpoint.IdempotencyHeader or
                SalesOrderLifecycleEndpoint.VersionHeader)
            {
                if (parameter is not Microsoft.OpenApi.OpenApiParameter mutableParameter)
                {
                    throw new InvalidOperationException(
                        $"OpenAPI header parameter {parameter.Name} cannot be made required.");
                }

                mutableParameter.Required = true;
                if (mutableParameter.Schema is Microsoft.OpenApi.OpenApiSchema headerSchema)
                {
                    if (parameter.Name == SalesOrderLifecycleEndpoint.IdempotencyHeader)
                    {
                        headerSchema.Format = "uuid";
                    }
                    else
                    {
                        headerSchema.Pattern = "^\"[1-9][0-9]*\"$";
                    }
                }
            }
            else if (parameter.Name == "action" &&
                     parameter.Schema is Microsoft.OpenApi.OpenApiSchema actionSchema)
            {
                actionSchema.Enum = SalesOrderLifecycleEndpoint.AllowedActions
                    .Select(action => System.Text.Json.Nodes.JsonValue.Create(action))
                    .ToArray();
            }
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationContextAccessor, HttpCorrelationContextAccessor>();
builder.Services.AddScoped<IExecutionScopeAccessor, HttpExecutionScopeAccessor>();
builder.Services.AddScoped<IRequestAuditContextAccessor, HttpRequestAuditContextAccessor>();

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestTelemetryMiddleware>();
if (!isOpenApiDocumentGeneration)
{
    app.UseAuthentication();
}
app.UseMiddleware<ApplicationScopeMiddleware>();
if (!isOpenApiDocumentGeneration)
{
    app.UseAuthorization();
}

app.MapKaguErpHealthEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().ExcludeFromDescription();
}
app.MapExecutionScopeEndpoint();
app.MapPartyReportQueryEndpoint();
app.MapSalesOrderLifecycleEndpoints();

app.Run();
