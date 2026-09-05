using System.Text.Json;

internal static class OpenApiContractCheck
{
    private static readonly string[] ExpectedSalesOrderActions =
        ["submit", "approve", "reject", "withdraw", "revise", "confirm", "cancel", "close"];
    private static readonly string[] ExpectedProblemProperties =
        ["type", "title", "status", "code", "traceId", "correlationId"];

    public static void Run(DirectoryInfo repositoryRoot)
    {
        string documentPath = Path.Combine(
            repositoryRoot.FullName,
            "docs",
            "openapi",
            "KaguERP.Api.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(documentPath));
        JsonElement root = document.RootElement;

        Assert(root.GetProperty("openapi").GetString()?.StartsWith("3.1", StringComparison.Ordinal) == true,
            "Generated API document is not OpenAPI 3.1.");

        JsonElement paths = root.GetProperty("paths");
        JsonElement bearerScheme = root
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        Assert(bearerScheme.GetProperty("type").GetString() == "http" &&
               bearerScheme.GetProperty("scheme").GetString() == "bearer",
            "OpenAPI bearer authentication scheme is missing or unstable.");
        AssertOperation(
            paths.GetProperty("/api/v1/sales-orders").GetProperty("post"),
            "CreateSalesOrderDraft",
            ["Idempotency-Key"],
            ["201", "400", "403", "409", "503"]);
        AssertOperation(
            paths.GetProperty("/api/v1/sales-orders/{orderId}").GetProperty("get"),
            "GetSalesOrderLifecycle",
            [],
            ["200", "400", "403", "404", "503"]);
        AssertOperation(
            paths.GetProperty("/api/v1/sales-orders/{orderId}/{action}").GetProperty("post"),
            "TransitionSalesOrder",
            ["Idempotency-Key", "If-Match"],
            ["200", "400", "403", "404", "409", "412", "422", "503"]);

        JsonElement transitionParameters = paths
            .GetProperty("/api/v1/sales-orders/{orderId}/{action}")
            .GetProperty("post")
            .GetProperty("parameters");
        JsonElement actionParameter = transitionParameters.EnumerateArray().Single(parameter =>
            parameter.GetProperty("name").GetString() == "action");
        string[] actions = actionParameter
            .GetProperty("schema")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert(actions.SequenceEqual(ExpectedSalesOrderActions),
            "OpenAPI sales-order transition action allowlist is missing or unstable.");

        JsonElement problemProperties = root
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ApiProblemResponse")
            .GetProperty("properties");
        foreach (string property in ExpectedProblemProperties)
        {
            Assert(problemProperties.TryGetProperty(property, out _),
                $"Problem Details schema is missing {property}.");
        }

        JsonElement schemas = root.GetProperty("components").GetProperty("schemas");
        JsonElement createSchema = schemas.GetProperty("SalesOrderCreateApiRequest");
        Assert(createSchema.GetProperty("required").EnumerateArray()
                .Select(item => item.GetString())
                .SequenceEqual(["companyId", "lines"]),
            "Sales order create contract does not require its authoritative lines.");
        JsonElement createLineSchema = schemas.GetProperty("SalesOrderLineCreateApiRequest");
        JsonElement responseLineSchema = schemas.GetProperty("SalesOrderLineApiResponse");
        Assert(createLineSchema.GetProperty("properties").GetProperty("orderedBaseQuantity")
                   .GetProperty("type").GetString() == "string" &&
               responseLineSchema.GetProperty("properties").GetProperty("orderedBaseQuantity")
                   .GetProperty("type").GetString() == "string",
            "Sales order quantities must remain decimal strings at the HTTP boundary.");

        AssertGeneratedClients(repositoryRoot);
    }

    private static void AssertGeneratedClients(DirectoryInfo repositoryRoot)
    {
        string typeScriptRoot = Path.Combine(repositoryRoot.FullName, "packages", "api-client-ts");
        string kotlinRoot = Path.Combine(repositoryRoot.FullName, "apps", "android", "generated", "api-client");

        AssertGeneratorVersion(typeScriptRoot);
        AssertGeneratorVersion(kotlinRoot);

        string typeScriptSalesApi = File.ReadAllText(Path.Combine(
            typeScriptRoot,
            "src",
            "apis",
            "SalesOrdersApi.ts"));
        Assert(typeScriptSalesApi.Contains("idempotencyKey: string;", StringComparison.Ordinal),
            "Generated TypeScript client does not require the idempotency header.");
        Assert(typeScriptSalesApi.Contains("ifMatch: string;", StringComparison.Ordinal),
            "Generated TypeScript client does not require the concurrency header.");
        Assert(typeScriptSalesApi.Contains("TransitionSalesOrderActionEnum", StringComparison.Ordinal),
            "Generated TypeScript client does not expose the sales transition allowlist.");
        Assert(typeScriptSalesApi.Contains("Authorization", StringComparison.Ordinal),
            "Generated TypeScript sales client does not apply bearer authentication.");
        string typeScriptCreateModel = File.ReadAllText(Path.Combine(
            typeScriptRoot,
            "src",
            "models",
            "SalesOrderCreateApiRequest.ts"));
        Assert(typeScriptCreateModel.Contains("lines: Array<SalesOrderLineCreateApiRequest>;", StringComparison.Ordinal),
            "Generated TypeScript client does not require authoritative sales-order lines.");

        string kotlinSalesApi = File.ReadAllText(Path.Combine(
            kotlinRoot,
            "src",
            "main",
            "kotlin",
            "com",
            "kagultd",
            "erp",
            "generated",
            "api",
            "apis",
            "SalesOrdersApi.kt"));
        Assert(kotlinSalesApi.Contains("idempotencyKey: java.util.UUID", StringComparison.Ordinal),
            "Generated Kotlin client does not type the idempotency header as UUID.");
        Assert(kotlinSalesApi.Contains("ifMatch: kotlin.String", StringComparison.Ordinal),
            "Generated Kotlin client does not require the concurrency header.");
        Assert(kotlinSalesApi.Contains("requiresAuthentication = true", StringComparison.Ordinal),
            "Generated Kotlin sales client does not require bearer authentication.");
        string kotlinCreateModel = File.ReadAllText(Path.Combine(
            kotlinRoot,
            "src",
            "main",
            "kotlin",
            "com",
            "kagultd",
            "erp",
            "generated",
            "api",
            "models",
            "SalesOrderCreateApiRequest.kt"));
        Assert(kotlinCreateModel.Contains("val lines: kotlin.collections.List<SalesOrderLineCreateApiRequest>",
                StringComparison.Ordinal),
            "Generated Kotlin client does not require authoritative sales-order lines.");

        Assert(!Directory.EnumerateFiles(typeScriptRoot, "*ApiResponseVersion*", SearchOption.AllDirectories).Any(),
            "Generated TypeScript client contains a stale inline version model.");
    }

    private static void AssertGeneratorVersion(string clientRoot)
    {
        string versionPath = Path.Combine(clientRoot, ".openapi-generator", "VERSION");
        Assert(File.Exists(versionPath) && File.ReadAllText(versionPath).Trim() == "7.24.0",
            $"Generated client at {clientRoot} was not produced by OpenAPI Generator 7.24.0.");
    }

    private static void AssertOperation(
        JsonElement operation,
        string operationId,
        IReadOnlyCollection<string> requiredHeaders,
        IReadOnlyCollection<string> responses)
    {
        Assert(operation.GetProperty("operationId").GetString() == operationId,
            $"OpenAPI operationId {operationId} is missing or unstable.");
        Assert(operation.GetProperty("security").EnumerateArray().Any(requirement =>
                requirement.TryGetProperty("Bearer", out _)),
            $"OpenAPI operation {operationId} does not require bearer authentication.");

        JsonElement[] parameters = operation.TryGetProperty("parameters", out JsonElement parameterArray)
            ? parameterArray.EnumerateArray().ToArray()
            : [];
        foreach (string header in requiredHeaders)
        {
            JsonElement? parameter = parameters
                .Where(item =>
                    item.GetProperty("in").GetString() == "header" &&
                    item.GetProperty("name").GetString() == header)
                .Select(item => (JsonElement?)item)
                .FirstOrDefault();
            Assert(parameter is not null &&
                   parameter.Value.TryGetProperty("required", out JsonElement required) &&
                   required.GetBoolean(),
                $"OpenAPI operation {operationId} does not require header {header}.");
        }

        JsonElement responseObject = operation.GetProperty("responses");
        foreach (string response in responses)
        {
            Assert(responseObject.TryGetProperty(response, out _),
                $"OpenAPI operation {operationId} is missing response {response}.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
