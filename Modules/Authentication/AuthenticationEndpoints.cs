using Microsoft.AspNetCore.Mvc;

namespace AgentAI.Modules.Authentication.Api;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").AllowAnonymous();

        group.MapPost("/sign-up", async (
            AuthenticationService service,
            SignUpRequest req) =>
        {
            await service.SignUpAsync(req.Email, req.Password);
            return Results.Ok();
        });

        group.MapPost("/sign-in", async (
            IConfiguration configuration,
            IWebHostEnvironment environment,
            AuthenticationService service,
            SignInRequest req) =>
        {
            if (UseDevelopmentLogin(configuration, environment))
                return Results.Ok(new SignInResult("dev-dashboard-token", "dev-dashboard-refresh-token"));

            var result = await service.SignInAsync(req.Email, req.Password);
            return Results.Ok(result);
        });

        group.MapPost("/confirm", async (
            AuthenticationService service,
            ConfirmUserRequest req) =>
        {
            await service.ConfirmUserAsync(req.Email, req.Code);
            return Results.Ok();
        });

        group.MapPost("/resend-confirmation", async (
            AuthenticationService service,
            ResendConfirmationRequest req) =>
        {
            await service.ResendConfirmationAsync(req.Email);
            return Results.Ok();
        });

        group.MapPost("/forgot-password", async (
            AuthenticationService service,
            ForgotPasswordRequest req) =>
        {
            await service.ForgotPasswordAsync(req.Email);
            return Results.Ok();
        });

        group.MapPost("/confirm-forgot-password", async (
            AuthenticationService service,
            ConfirmForgotPasswordRequest req) =>
        {
            await service.ConfirmForgotPasswordAsync(req.Email, req.Code, req.NewPassword);
            return Results.Ok();
        });

        group.MapPost("/sign-out", async (
            AuthenticationService service,
            SignOutRequest req) =>
        {
            await service.SignOutAsync(req.AccessToken);
            return Results.Ok();
        });

        group.MapGet("/validate", async (
            AuthenticationService service,
            [FromHeader(Name = "Authorization")] string authHeader) =>
        {
            var token = authHeader.Replace("Bearer ", "");
            var valid = await service.ValidateTokenAsync(token);

            return valid ? Results.Ok() : Results.Unauthorized();
        });

        return app;
    }

    private static bool UseDevelopmentLogin(IConfiguration configuration, IWebHostEnvironment environment)
        => environment.IsDevelopment() &&
            (configuration.GetValue("Authentication:DevLoginEnabled", false) ||
             string.IsNullOrWhiteSpace(configuration["Cognito:ClientId"]));
}
