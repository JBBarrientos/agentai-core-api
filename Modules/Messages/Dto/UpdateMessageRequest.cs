namespace AgentAI.Modules.Messages.Dto;

public record UpdateMessageRequest(
    string? SenderType,
    string? SenderName,
    string? Body,
    string? MessageType
);