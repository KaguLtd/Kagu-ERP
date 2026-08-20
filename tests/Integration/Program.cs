using KaguERP.DatabaseIntegrationChecks;

if (args is ["seed-auth-smoke"])
{
    return await AuthSmokeFixture.SeedAsync();
}

if (args is ["cleanup-auth-smoke"])
{
    return await AuthSmokeFixture.CleanupAsync();
}

return await DatabaseIntegrationCheck.RunAsync();
