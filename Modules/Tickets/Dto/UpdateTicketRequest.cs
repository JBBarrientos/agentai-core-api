namespace AgentAI.Modules.Tickets.Dto;

public record UpdateTicketRequest(
    string? Title,
    string? Description,
    int? State,
    string? StateLabel,
    int? Priority,
    string? PriorityLabel,
    string? AssignedTo,
    string? AssignmentGroup,
    DateTime? ResolvedAt
);