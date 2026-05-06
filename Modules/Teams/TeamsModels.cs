namespace AgentAI.Modules.Teams;

public sealed record TeamsUser(
    string Id,
    string DisplayName,
    string Email
);

public sealed record TeamsNotificationMessage(
    string RecipientEmail,
    string Subject,
    string Body,
    string? TicketNumber = null,
    string? TicketSysId = null
);

public sealed record TeamsNotificationResult(
    bool Sent,
    string Provider,
    string Message,
    string? RecipientEmail = null,
    string? RecipientUserId = null
);

public sealed record TeamsTokenResponse(
    string TokenType,
    int ExpiresIn,
    string AccessToken
);
