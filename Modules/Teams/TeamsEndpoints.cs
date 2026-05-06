namespace AgentAI.Modules.Teams;

public static class TeamsEndpoints
{
    public static IEndpointRouteBuilder MapTeamsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/teams")
            .WithTags("Teams")
            .AllowAnonymous();

        group.MapPost("/notifications/test", async (
            SendTeamsTestNotificationRequest request,
            ITeamsNotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.SendTestAsync(request.RecipientEmail, request.Message, ct);
            return Results.Ok(result);
        });

        group.MapPost("/notifications/tickets/{sysId}/review-started", async (
            string sysId,
            ITeamsNotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.NotifyReviewStartedAsync(sysId, ct);
            return Results.Ok(result);
        });

        group.MapPost("/notifications/tickets/{sysId}/resolved", async (
            string sysId,
            string? summary,
            ITeamsNotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.NotifyResolvedAsync(sysId, summary, ct);
            return Results.Ok(result);
        });

        group.MapPost("/notifications/tickets/{sysId}/escalated", async (
            string sysId,
            string? reason,
            ITeamsNotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.NotifyEscalatedAsync(sysId, reason, ct);
            return Results.Ok(result);
        });

        return app;
    }
}
