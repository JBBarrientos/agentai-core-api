namespace AgentAI.Modules.Notifications;

public interface INotificationSender
{
    Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken ct = default);
}
