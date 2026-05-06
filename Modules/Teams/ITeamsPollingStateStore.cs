namespace AgentAI.Modules.Teams;

public interface ITeamsPollingStateStore
{
    Task<TeamsPollingState> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(TeamsPollingState state, CancellationToken ct = default);
}

public sealed class TeamsPollingState
{
    public DateTime? LastProcessedOpenedAtUtc { get; set; }
    public string LastProcessedTicketSysId { get; set; } = string.Empty;
    public string LastProcessedTicketNumber { get; set; } = string.Empty;
    public HashSet<string> ProcessedTicketSysIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsProcessed(string sysId)
        => !string.IsNullOrWhiteSpace(sysId) && ProcessedTicketSysIds.Contains(sysId);

    public void MarkProcessed(string sysId, string number, DateTime? openedAtUtc)
    {
        if (!string.IsNullOrWhiteSpace(sysId))
            ProcessedTicketSysIds.Add(sysId);

        if (openedAtUtc is null)
            return;

        if (LastProcessedOpenedAtUtc is null || openedAtUtc > LastProcessedOpenedAtUtc)
        {
            LastProcessedOpenedAtUtc = openedAtUtc;
            LastProcessedTicketSysId = sysId;
            LastProcessedTicketNumber = number;
        }
    }
}
