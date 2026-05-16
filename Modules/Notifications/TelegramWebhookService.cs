using System.Text;
using System.Text.RegularExpressions;
using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.Notifications;

public sealed partial class TelegramWebhookService : ITelegramWebhookService
{
    private readonly IServiceNowConnector _serviceNow;
    private readonly ITelegramMessageSender _messageSender;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(
        IServiceNowConnector serviceNow,
        ITelegramMessageSender messageSender,
        ILogger<TelegramWebhookService> logger)
    {
        _serviceNow = serviceNow;
        _messageSender = messageSender;
        _logger = logger;
    }

    public async Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
    {
        var message = update.Message;
        if (message?.Chat is null)
            return;

        var chatId = message.Chat.Id.ToString();
        var text = message.Text?.Trim();

        _logger.LogInformation(
            "Telegram webhook received message. ChatId={ChatId} Text={Text}",
            chatId,
            string.IsNullOrWhiteSpace(text) ? "(empty)" : text);

        if (string.IsNullOrWhiteSpace(text))
        {
            await _messageSender.SendToChatAsync(chatId, "Enviame el numero de ticket, por ejemplo INC0010024.", ct: ct);
            return;
        }

        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _messageSender.SendToChatAsync(chatId, "Hola, soy AgentAI. Enviame tu numero de ticket, por ejemplo INC0010024, y busco el estado del caso.", ct: ct);
            return;
        }

        var ticketNumber = ExtractTicketNumber(text);
        if (ticketNumber is null)
        {
            await _messageSender.SendToChatAsync(chatId, "No pude identificar un numero de ticket. Enviame algo con INC, por ejemplo INC0010024 o inc10024.", ct: ct);
            return;
        }

        try
        {
            var incident = await _serviceNow.GetIncidentByNumberAsync(ticketNumber, ct);
            var response = incident is null
                ? $"No encontre el ticket {ticketNumber}. Verifica el numero e intenta nuevamente."
                : BuildTicketSummary(incident);

            var result = await _messageSender.SendToChatAsync(chatId, response, ct: ct);
            _logger.LogInformation(
                "Telegram ticket lookup response sent. ChatId={ChatId} TicketNumber={TicketNumber} Sent={Sent} Message={Message}",
                chatId,
                ticketNumber,
                result.Sent,
                result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook failed while processing ticket {TicketNumber}.", ticketNumber);
            await _messageSender.SendToChatAsync(chatId, "No pude consultar ServiceNow en este momento. Intenta nuevamente mas tarde.", ct: ct);
        }
    }

    private static string? ExtractTicketNumber(string text)
    {
        var match = TicketNumberRegex().Match(text);
        return match.Success
            ? string.Concat(match.Value.ToUpperInvariant().Where(char.IsLetterOrDigit))
            : null;
    }

    private static string BuildTicketSummary(ServiceNowIncident incident)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Ticket {incident.Number}");
        builder.AppendLine($"Estado: {incident.StateLabel}");
        builder.AppendLine($"Prioridad: {incident.PriorityLabel}");

        if (!string.IsNullOrWhiteSpace(incident.Title))
            builder.AppendLine($"Asunto: {incident.Title}");

        if (incident.UpdatedAt is not null)
            builder.AppendLine($"Ultima actualizacion: {incident.UpdatedAt:yyyy-MM-dd HH:mm} UTC");

        if (!string.IsNullOrWhiteSpace(incident.Description))
        {
            var description = incident.Description.Length > 500
                ? incident.Description[..500] + "..."
                : incident.Description;
            builder.AppendLine();
            builder.AppendLine(description);
        }

        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"\bINC[\s-]*\d{4,12}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketNumberRegex();
}
