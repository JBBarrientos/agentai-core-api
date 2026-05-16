namespace AgentAI.Modules.Notifications;

public sealed record NotificationMessage(
    string RecipientEmail,
    string Subject,
    string Body,
    string? TicketNumber = null,
    string? TicketSysId = null
);

public sealed record NotificationResult(
    bool Sent,
    string Provider,
    string Message,
    string? RecipientEmail = null
);
