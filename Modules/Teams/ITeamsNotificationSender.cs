namespace AgentAI.Modules.Teams;

public interface ITeamsNotificationSender
{
    Task<TeamsNotificationResult> SendAsync(TeamsNotificationMessage message, CancellationToken ct = default);
}
