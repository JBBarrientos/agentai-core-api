public record IncomingMessageRequest(
    int ConversationId,
    int? TicketId,
    string SysId,
    string SenderType,
    string SenderName,
    string Body,
    string MessageType,
    DateTime SentAt
);