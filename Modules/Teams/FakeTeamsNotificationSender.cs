namespace AgentAI.Modules.Teams;

public sealed class FakeTeamsNotificationSender : ITeamsNotificationSender
{
    private readonly ILogger<FakeTeamsNotificationSender> _logger;

    public FakeTeamsNotificationSender(ILogger<FakeTeamsNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task<TeamsNotificationResult> SendAsync(TeamsNotificationMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Teams fake] To={RecipientEmail} Ticket={TicketNumber} Subject={Subject} Body={Body}",
            message.RecipientEmail,
            message.TicketNumber,
            message.Subject,
            message.Body);

        return Task.FromResult(new TeamsNotificationResult(
            Sent: true,
            Provider: "FakeTeams",
            Message: "Notification logged. Real Teams sending is still disabled.",
            RecipientEmail: message.RecipientEmail));
    }
}
