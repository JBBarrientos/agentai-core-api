using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Amazon;
using AgentAI.Modules.Authentication.Api;
using AgentAI.Modules.Authentication.Cognito;
using AgentAI.Modules.Users;

namespace AgentAI.Modules.Authentication;

public static class AuthenticationModule
{
    public static IServiceCollection AddAuthenticationModule(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<CognitoOptions>(config.GetSection("Cognito"));

        services.AddSingleton<IAmazonCognitoIdentityProvider>(_ =>
            new AmazonCognitoIdentityProviderClient(
                new BasicAWSCredentials(
                    config["AWS:AccessKeyId"],
                    config["AWS:SecretAccessKey"]
                ),
                RegionEndpoint.GetBySystemName(config["AWS:Region"])
            )
        );

        services.AddScoped<IAuthenticationProvider, CognitoAuthenticationProvider>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }

    public static IEndpointRouteBuilder MapAuthenticationModule(this IEndpointRouteBuilder app)
    {
        app.MapAuthenticationEndpoints();
        return app;
    }
}