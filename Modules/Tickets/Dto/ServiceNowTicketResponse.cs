namespace AgentAI.Modules.Tickets.Dto;

public sealed record ServiceNowTicketResponse(
    string SysId,
    string Number,
    string Title,
    string Description,
    int State,
    string StateLabel,
    int Priority,
    string PriorityLabel,
    string CreatedByName,
    string CreatedByEmail,
    DateTime? OpenedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt,
    DateTime LastSyncedAt
);
