namespace AgentAI.Modules.Messages.Dto;
public record IncomingMessageResponse(
    int MessageId,
    int ConversationId,
    string ConversationSysId,
    bool ConversationCreated,
    string SysId,
    string SenderType,
    string SenderName,
    string Body,
    string MessageType,
    DateTime SentAt,
    DateTime LastSyncedAt
);