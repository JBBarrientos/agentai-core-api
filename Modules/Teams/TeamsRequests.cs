namespace AgentAI.Modules.Teams;

public sealed record SendTeamsTestNotificationRequest(
    string RecipientEmail,
    string Message
);
