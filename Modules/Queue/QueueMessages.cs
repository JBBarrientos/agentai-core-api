namespace AgentAI.Modules.Queue;
public record InboundMessage(
    string TicketId,
    string CorrelationId,
    string CustomerId,
    Dictionary<string, string> Metadata
);
public record OutboundMessage(
    string TicketId,
    string CorrelationId,
    bool Found,
    string? TargetAgent,
    string? Action,
    string? Payload
);
