namespace AgentAI.Modules.Conversations.Dto;

public record CreateConversationRequest(
    string SysId,
    int TicketId,
    string Channel,
    string Status,
    DateTime StartedAt
);