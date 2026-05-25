namespace AgentAI.Modules.Notifications;

public interface ITelegramMessageSender
{
    Task<NotificationResult> SendToChatAsync(string chatId, string text, string? recipientEmail = null, CancellationToken ct = default);
}
