namespace AgentAI.Modules.Queue;
public record InboundMessage(
    string TicketId,
    string CorrelationId,
    string CustomerId,
    string Action,
    string? Payload
);

public record OutboundMessage(
    string TicketId,
    string CorrelationId,
    string CustomerId,
    string Action,
    string? Payload,
    string? TargetAgent
);