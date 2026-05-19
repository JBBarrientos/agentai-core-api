namespace AgentAI.Modules.Notifications;

public interface ITelegramWebhookService
{
    Task HandleAsync(TelegramUpdate update, CancellationToken ct = default);
}
