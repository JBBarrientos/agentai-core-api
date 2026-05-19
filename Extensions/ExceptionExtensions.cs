using Amazon.CognitoIdentityProvider.Model;
using Microsoft.AspNetCore.Diagnostics;

namespace AgentAI.Extensions;

public static class ExceptionExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                var (status, message) = ex switch
                {
                    NotAuthorizedException => (StatusCodes.Status401Unauthorized, "Incorrect username or password."),
                    UserNotFoundException => (StatusCodes.Status404NotFound, "User not found."),
                    UsernameExistsException => (StatusCodes.Status409Conflict, "User already exists."),
                    UserNotConfirmedException => (StatusCodes.Status403Forbidden, "User is not confirmed."),
                    CodeMismatchException => (StatusCodes.Status400BadRequest, "Invalid confirmation code."),
                    ExpiredCodeException => (StatusCodes.Status400BadRequest, "Confirmation code has expired."),
                    _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
                };

                if (status == StatusCodes.Status500InternalServerError &&
                    app.ApplicationServices.GetRequiredService<IHostEnvironment>().IsDevelopment() &&
                    ex is not null)
                {
                    message = ex.Message;
                }

                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = message });
            });
        });

        return app;
    }
}
