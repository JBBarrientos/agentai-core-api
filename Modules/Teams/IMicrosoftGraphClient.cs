namespace AgentAI.Modules.Teams;

public interface IMicrosoftGraphClient
{
    Task<TeamsUser?> GetUserByEmailAsync(string email, CancellationToken ct = default);
}
