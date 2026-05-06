using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.Teams;

public sealed class TeamsNotificationService : ITeamsNotificationService
{
    private readonly IServiceNowConnector _serviceNowConnector;
    private readonly IMicrosoftGraphClient _graphClient;
    private readonly ITeamsNotificationSender _sender;
    private readonly IConfiguration _configuration;

    public TeamsNotificationService(
        IServiceNowConnector serviceNowConnector,
        IMicrosoftGraphClient graphClient,
        ITeamsNotificationSender sender,
        IConfiguration configuration)
    {
        _serviceNowConnector = serviceNowConnector;
        _graphClient = graphClient;
        _sender = sender;
        _configuration = configuration;
    }

    public async Task<TeamsNotificationResult> SendTestAsync(string recipientEmail, string message, CancellationToken ct = default)
    {
        var user = await TryResolveGraphUserAsync(recipientEmail, ct);

        var result = await _sender.SendAsync(new TeamsNotificationMessage(
            recipientEmail,
            "AgentAI test notification",
            message), ct);

        return result with { RecipientUserId = user?.Id };
    }

    public async Task<TeamsNotificationResult> NotifyReviewStartedAsync(string serviceNowSysId, CancellationToken ct = default)
    {
        var ticket = await GetTicketOrThrowAsync(serviceNowSysId, ct);
        var body = $"Hola {GetDisplayName(ticket)}, estamos revisando tu caso {ticket.Number}: \"{ticket.Title}\".";

        return await SendTicketNotificationAsync(ticket, "Ticket en revision", body, ct);
    }

    public async Task<TeamsNotificationResult> NotifyResolvedAsync(string serviceNowSysId, string? resolutionSummary = null, CancellationToken ct = default)
    {
        var ticket = await GetTicketOrThrowAsync(serviceNowSysId, ct);
        var summary = string.IsNullOrWhiteSpace(resolutionSummary)
            ? "El caso fue marcado como resuelto."
            : resolutionSummary.Trim();
        var body = $"Hola {GetDisplayName(ticket)}, tu caso {ticket.Number} fue resuelto. {summary}";

        return await SendTicketNotificationAsync(ticket, "Ticket resuelto", body, ct);
    }

    public async Task<TeamsNotificationResult> NotifyEscalatedAsync(string serviceNowSysId, string? reason = null, CancellationToken ct = default)
    {
        var ticket = await GetTicketOrThrowAsync(serviceNowSysId, ct);
        var escalationReason = string.IsNullOrWhiteSpace(reason)
            ? "Necesita revision de soporte humano."
            : reason.Trim();
        var body = $"Hola {GetDisplayName(ticket)}, tu caso {ticket.Number} fue derivado a soporte. {escalationReason}";

        return await SendTicketNotificationAsync(ticket, "Ticket derivado a soporte", body, ct);
    }

    private async Task<TeamsNotificationResult> SendTicketNotificationAsync(ServiceNowIncident ticket, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ticket.CreatedByEmail))
            return new TeamsNotificationResult(false, "Teams", $"Ticket {ticket.Number} has no requester email.");

        var user = await TryResolveGraphUserAsync(ticket.CreatedByEmail, ct);

        var result = await _sender.SendAsync(new TeamsNotificationMessage(
            ticket.CreatedByEmail,
            subject,
            body,
            ticket.Number,
            ticket.SysId), ct);

        return result with { RecipientUserId = user?.Id };
    }

    private async Task<TeamsUser?> TryResolveGraphUserAsync(string email, CancellationToken ct)
    {
        if (!_configuration.GetValue("MicrosoftGraph:ResolveUsersEnabled", false))
            return null;

        return await _graphClient.GetUserByEmailAsync(email, ct);
    }

    private async Task<ServiceNowIncident> GetTicketOrThrowAsync(string serviceNowSysId, CancellationToken ct)
        => await _serviceNowConnector.GetIncidentAsync(serviceNowSysId, ct)
            ?? throw new InvalidOperationException($"ServiceNow ticket {serviceNowSysId} was not found.");

    private static string GetDisplayName(ServiceNowIncident ticket)
        => string.IsNullOrWhiteSpace(ticket.CreatedByName) ? "buenas" : ticket.CreatedByName;
}
