using System.Text.Json;
using AgentAI.Modules.Messages;
using AgentAI.Modules.Messages.Dto;

namespace AgentAI.Modules.Queue;

public class ActionDispatcher
{
    private readonly IIncomingMessageService _incomingMessageService;
    private readonly IQueueService _queueService;
    private readonly ILogger<ActionDispatcher> _logger;

    public ActionDispatcher(
        IIncomingMessageService incomingMessageService,
        [FromKeyedServices("inbound")] IQueueService queueService,
        ILogger<ActionDispatcher> logger)
    {
        _incomingMessageService = incomingMessageService;
        _queueService = queueService;
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
            "send_email" => HandleEmailAsync(message),
            "agente_enrutador" => HandleAgenteEnrutadorAsync(message),
            "agente_accion" => HandleAgenteAccionAsync(message),
            _ => HandleUnknownAsync(message)
        };
    }


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


        await _incomingMessageService.ProcessOutboundAsync(payload);

        _logger.LogInformation("[SEND_MESSAGE] Persisted bot response for ticket {TicketId}", message.TicketId);
    }

    private Task HandleEmailAsync(OutboundMessage message)
    {
        _logger.LogInformation("[EMAIL] Would send email for ticket {TicketId}", message.TicketId);
        return Task.CompletedTask;
    }

    private async Task HandleAgenteEnrutadorAsync(OutboundMessage message, CancellationToken ct = default)
    {
        var inboundMessage = new InboundMessage(
            TicketId: message.TicketId,
            CorrelationId: message.CorrelationId,
            CustomerId: message.CustomerId,
            Action: "agente_enrutador",
            Payload: message.Payload
        );

        await _queueService.SendMessageAsync(JsonSerializer.Serialize(inboundMessage), ct);

        _logger.LogInformation("[AGENTE_ENRUTADOR] Routed ticket {TicketId} to outbound queue", message.TicketId);
    }

    private async Task HandleAgenteAccionAsync(OutboundMessage message, CancellationToken ct = default)
    {
        var inboundMessage = new InboundMessage(
            TicketId: message.TicketId,
            CorrelationId: message.CorrelationId,
            CustomerId: message.CustomerId,
            Action: "ticket_para_ejecutar",
            Payload: message.Payload
        );

        await _queueService.SendMessageAsync(JsonSerializer.Serialize(inboundMessage), ct);

        _logger.LogInformation("[AGENTE_ACCION] Routed ticket {TicketId} to inbound queue", message.TicketId);
    }


    private Task HandleUnknownAsync(OutboundMessage message)
    {
        _logger.LogWarning("[UNKNOWN] Unknown action {Action} for ticket {TicketId}", message.Action, message.TicketId);
        return Task.CompletedTask;
    }
}