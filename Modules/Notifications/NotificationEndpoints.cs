namespace AgentAI.Modules.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications")
            .WithTags("Notifications")
            .AllowAnonymous();

        group.MapPost("/test", async (
            SendTestNotificationRequest request,
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.SendTestAsync(request.RecipientEmail, request.Message, ct);
            return Results.Ok(result);
        });

        group.MapPost("/tickets/{sysId}/review-started", async (
            string sysId,
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.NotifyReviewStartedAsync(sysId, ct);
            return Results.Ok(result);
        });

        group.MapPost("/tickets/{sysId}/resolved", async (
            string sysId,
            string? summary,
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.NotifyResolvedAsync(sysId, summary, ct);
            return Results.Ok(result);
        });

        group.MapPost("/tickets/{sysId}/escalated", async (
            string sysId,
            string? reason,
            INotificationService service,
            CancellationToken ct) =>
        {
            var result = await service.NotifyEscalatedAsync(sysId, reason, ct);
            return Results.Ok(result);
        });

        app.MapPost("/telegram/webhook", async (
            TelegramUpdate update,
            ITelegramWebhookService service,
            CancellationToken ct) =>
        {
            await service.HandleAsync(update, ct);
            return Results.Ok();
        })
        .WithTags("Telegram")
        .AllowAnonymous();

        return app;
    }
}
