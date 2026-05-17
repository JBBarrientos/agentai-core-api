using System.Text.Json;
using AgentAI.Modules.Messages;
using AgentAI.Modules.Messages.Dto;

namespace AgentAI.Modules.Queue;

public class ActionDispatcher
{
    private readonly IIncomingMessageService _incomingMessageService;
    private readonly ILogger<ActionDispatcher> _logger;

    public ActionDispatcher(
        IIncomingMessageService incomingMessageService,
        ILogger<ActionDispatcher> logger)
    {
        _incomingMessageService = incomingMessageService;
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
            "send_message" => HandleSendMessageAsync(message),
            "send_whatsapp" => HandleWhatsAppAsync(message),
            "send_email" => HandleEmailAsync(message),
            "escalate" => HandleEscalationAsync(message),
            _ => HandleUnknownAsync(message)
        };
    }

    private record OutboundMessagePayload(int ConversationId, string Body, string MessageType);

    private async Task HandleSendMessageAsync(OutboundMessage message)
    {
        if (string.IsNullOrEmpty(message.Payload))
        {
            _logger.LogWarning("[SEND_MESSAGE] No payload for ticket {TicketId}", message.TicketId);
            return;
        }

        var payload = JsonSerializer.Deserialize<OutboundMessagePayload>(message.Payload);
        if (payload is null)
        {
            _logger.LogWarning("[SEND_MESSAGE] Could not deserialize payload for ticket {TicketId}", message.TicketId);
            return;
        }

        await _incomingMessageService.ProcessOutboundAsync(new IncomingMessageRequest(
            ConversationId: payload.ConversationId,
            TicketId: null,
            SysId: Guid.NewGuid().ToString(),
            SenderType: "bot",
            SenderName: "Agent",
            Body: payload.Body,
            MessageType: payload.MessageType,
            SentAt: DateTime.UtcNow
        ));

        _logger.LogInformation("[SEND_MESSAGE] Persisted bot response for ticket {TicketId}", message.TicketId);
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