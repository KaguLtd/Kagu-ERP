using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace KaguERP.Api.Security;

internal static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddKaguErpAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string authority = GetRequiredAbsoluteUri(configuration, "Authentication:Authority");
        string audience = GetRequiredValue(configuration, "Authentication:Audience");
        bool requireHttpsMetadata = configuration.GetValue("Authentication:RequireHttpsMetadata", true);

        if (requireHttpsMetadata && !authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Authentication:Authority must use HTTPS when metadata HTTPS is required.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority.TrimEnd('/');
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static string GetRequiredAbsoluteUri(IConfiguration configuration, string key)
    {
        string value = GetRequiredValue(configuration, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"Configuration value {key} must be an absolute URI.");
        }

        return value;
    }

    private static string GetRequiredValue(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required configuration value {key} is missing.");
        }

        return value;
    }
}
