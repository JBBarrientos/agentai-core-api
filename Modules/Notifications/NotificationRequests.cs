namespace AgentAI.Modules.Notifications;

public sealed record SendTestNotificationRequest(
    string RecipientEmail,
    string Message
);
