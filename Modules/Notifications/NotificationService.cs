using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly IServiceNowConnector _serviceNowConnector;
    private readonly INotificationSender _sender;
    private readonly IConfiguration _configuration;

    public NotificationService(
        IServiceNowConnector serviceNowConnector,
        INotificationSender sender,
        IConfiguration configuration)
    {
        _serviceNowConnector = serviceNowConnector;
        _sender = sender;
        _configuration = configuration;
    }

    public async Task<NotificationResult> SendTestAsync(string recipientEmail, string message, CancellationToken ct = default)
    {
        return await _sender.SendAsync(new NotificationMessage(
            recipientEmail,
            "AgentAI test notification",
            message), ct);
    }

    public async Task<NotificationResult> NotifyReviewStartedAsync(string serviceNowSysId, CancellationToken ct = default)
    {
        var ticket = await GetTicketOrThrowAsync(serviceNowSysId, ct);
        var body = $"Hola {GetDisplayName(ticket)}, estamos revisando tu caso {ticket.Number}: \"{ticket.Title}\".";
        var workNote = $"AgentAI comenzo a revisar el caso {ticket.Number}.";

        await _serviceNowConnector.MarkInProgressAsync(ticket.SysId, workNote, body, ct);

        return await SendTicketNotificationAsync(ticket, "Ticket en revision", body, ct);
    }

    public async Task<NotificationResult> NotifyResolvedAsync(string serviceNowSysId, string? resolutionSummary = null, CancellationToken ct = default)
    {
        var ticket = await GetTicketOrThrowAsync(serviceNowSysId, ct);
        var summary = string.IsNullOrWhiteSpace(resolutionSummary)
            ? "El caso fue marcado como resuelto."
            : resolutionSummary.Trim();
        var body = $"Hola {GetDisplayName(ticket)}, tu caso {ticket.Number} fue resuelto. {summary}";
        var workNote = $"AgentAI resolvio el caso {ticket.Number}.";

        await _serviceNowConnector.ResolveIncidentAsync(ticket.SysId, summary, workNote, ct: ct);

        return await SendTicketNotificationAsync(ticket, "Ticket resuelto", body, ct);
    }

    public async Task<NotificationResult> NotifyEscalatedAsync(string serviceNowSysId, string? reason = null, CancellationToken ct = default)
    {
        var ticket = await GetTicketOrThrowAsync(serviceNowSysId, ct);
        var escalationReason = string.IsNullOrWhiteSpace(reason)
            ? "Necesita revision de soporte humano."
            : reason.Trim();
        var body = $"Hola {GetDisplayName(ticket)}, tu caso {ticket.Number} fue derivado a soporte. {escalationReason}";
        var workNote = $"AgentAI derivo el caso {ticket.Number} a soporte. Motivo: {escalationReason}";
        var assignmentGroupSysId = _configuration["ServiceNow:EscalationAssignmentGroupSysId"];

        if (string.IsNullOrWhiteSpace(assignmentGroupSysId))
        {
            await _serviceNowConnector.AddWorkNoteAsync(ticket.SysId, workNote, ct);
            await _serviceNowConnector.AddCustomerCommentAsync(ticket.SysId, body, ct);
        }
        else
        {
            await _serviceNowConnector.EscalateIncidentAsync(ticket.SysId, assignmentGroupSysId, workNote, body, ct);
        }

        return await SendTicketNotificationAsync(ticket, "Ticket derivado a soporte", body, ct);
    }

    private async Task<NotificationResult> SendTicketNotificationAsync(ServiceNowIncident ticket, string subject, string body, CancellationToken ct)
    {
        var recipient = string.IsNullOrWhiteSpace(ticket.CreatedByEmail)
            ? "default"
            : ticket.CreatedByEmail;

        return await _sender.SendAsync(new NotificationMessage(
            recipient,
            subject,
            body,
            ticket.Number,
            ticket.SysId), ct);
    }

    private async Task<ServiceNowIncident> GetTicketOrThrowAsync(string serviceNowSysId, CancellationToken ct)
        => await _serviceNowConnector.GetIncidentAsync(serviceNowSysId, ct)
            ?? throw new InvalidOperationException($"ServiceNow ticket {serviceNowSysId} was not found.");

    private static string GetDisplayName(ServiceNowIncident ticket)
        => string.IsNullOrWhiteSpace(ticket.CreatedByName) ? "buenas" : ticket.CreatedByName;
}
