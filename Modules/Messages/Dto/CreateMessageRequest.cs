namespace AgentAI.Modules.Messages.Dto;

public record CreateMessageRequest(
    string SysId,
    int ConversationId,
    string SenderType,
    string SenderName,
    string Body,
    string MessageType,
    DateTime SentAt
);