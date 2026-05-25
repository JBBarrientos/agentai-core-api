namespace AgentAI.Modules.Messages.Dto;
public record IncomingMessageRequest(
    int TicketId,
    string SysId,
    string ConversationSysId,
    string SenderType,
    string SenderName,
    string Body,
    string MessageType,
    DateTime SentAt
);