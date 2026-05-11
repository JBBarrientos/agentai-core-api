namespace AgentAI.Modules.Queue;
public class ActionDispatcher
{
    private readonly ILogger<ActionDispatcher> _logger;

    public ActionDispatcher(ILogger<ActionDispatcher> logger)
    {
        _logger = logger;
    }

    public Task DispatchAsync(OutboundMessage message)
    {
        _logger.LogInformation(
            "Dispatching action — TicketId: {TicketId}, Action: {Action}",
            message.TicketId,
            message.Action);

        return message.Action switch
        {
            "send_whatsapp" => HandleWhatsAppAsync(message),
            "send_email" => HandleEmailAsync(message),
            "escalate" => HandleEscalationAsync(message),
            _ => HandleUnknownAsync(message)
        };
    }

    private Task HandleWhatsAppAsync(OutboundMessage message)
    {
        _logger.LogInformation("[WHATSAPP] Would send message for ticket {TicketId}", message.TicketId);
        return Task.CompletedTask;
    }

    private Task HandleEmailAsync(OutboundMessage message)
    {
        _logger.LogInformation("[EMAIL] Would send email for ticket {TicketId}", message.TicketId);
        return Task.CompletedTask;
    }

    private Task HandleEscalationAsync(OutboundMessage message)
    {
        _logger.LogInformation("[ESCALACION] Would escalate ticket {TicketId}", message.TicketId);
        return Task.CompletedTask;
    }

    private Task HandleUnknownAsync(OutboundMessage message)
    {
        _logger.LogWarning("[UNKNOWN] Unknown action {Action} for ticket {TicketId}", message.Action, message.TicketId);
        return Task.CompletedTask;
    }
}