namespace AgentAI.Modules.Notifications;

public interface INotificationService
{
    Task<NotificationResult> SendTestAsync(string recipientEmail, string message, CancellationToken ct = default);
    Task<NotificationResult> NotifyReviewStartedAsync(string serviceNowSysId, CancellationToken ct = default);
    Task<NotificationResult> NotifyResolvedAsync(string serviceNowSysId, string? resolutionSummary = null, CancellationToken ct = default);
    Task<NotificationResult> NotifyEscalatedAsync(string serviceNowSysId, string? reason = null, CancellationToken ct = default);
}
