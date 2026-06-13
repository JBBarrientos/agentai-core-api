using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens; 

namespace AgentAI.Extensions;
public static class AuthExtensions
{
    public static IServiceCollection AddCognitoAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var region = configuration["AWS:Region"];
        var userPoolId = configuration["Cognito:UserPoolId"];
        var cognitoConfigured = !string.IsNullOrWhiteSpace(region) && !string.IsNullOrWhiteSpace(userPoolId);

        if (cognitoConfigured)
        {
            var issuer = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = issuer;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateLifetime = true,
                        ValidateAudience = false,
                    };
                });

            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
        }

        return services;
    }
}