namespace AgentAI.Modules.Teams;

public interface ITeamsNotificationService
{
    Task<TeamsNotificationResult> SendTestAsync(string recipientEmail, string message, CancellationToken ct = default);
    Task<TeamsNotificationResult> NotifyReviewStartedAsync(string serviceNowSysId, CancellationToken ct = default);
    Task<TeamsNotificationResult> NotifyResolvedAsync(string serviceNowSysId, string? resolutionSummary = null, CancellationToken ct = default);
    Task<TeamsNotificationResult> NotifyEscalatedAsync(string serviceNowSysId, string? reason = null, CancellationToken ct = default);
}
