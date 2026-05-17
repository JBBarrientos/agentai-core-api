namespace AgentAI.Modules.Messages.Dto;

public record IncomingMessageRequest(
    string ConversationSysId,
    int? TicketId,
    string SysId,
    string SenderType,
    string SenderName,
    string Body,
    string MessageType,
    DateTime SentAt
);