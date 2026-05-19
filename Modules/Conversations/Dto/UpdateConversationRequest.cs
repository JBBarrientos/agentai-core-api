namespace AgentAI.Modules.Conversations.Dto;

public record UpdateConversationRequest(
    string? Channel,
    string? Status,
    DateTime? EndedAt
);