using System.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using AgentAI.Modules.Messages;
using AgentAI.Modules.Tickets;
using AgentAI.Modules.Messages.Dto;
using AgentAI.Modules.Conversations;

namespace AgentAI.Modules.Notifications;

public sealed partial class TelegramWebhookService : ITelegramWebhookService
{
    private readonly IConfiguration _configuration;
    private readonly ITelegramMessageSender _messageSender;
    private readonly IIncomingMessageService _incomingMessageService;
    private readonly ITicketService _ticketService;
    private readonly IConversationService _conversationService;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(
        IConfiguration configuration,
        ITelegramMessageSender messageSender,
        IIncomingMessageService incomingMessageService,
        ITicketService ticketService,
        IConversationService conversationService,
        ILogger<TelegramWebhookService> logger)
    {
        _configuration = configuration;
        _messageSender = messageSender;
        _incomingMessageService = incomingMessageService;
        _ticketService = ticketService;
        _conversationService = conversationService;
        _logger = logger;
    }

    public async Task HandleAsync(TelegramUpdate update, CancellationToken ct = default)
    {
        var message = update.Message;
        if (message?.Chat is null)
            return;
        var chatId = message.Chat.Id.ToString();
        var text = message.Text?.Trim();

        if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
        {
            await _messageSender.SendToChatAsync(
                chatId,
                "Hola, soy AgentAI. Enviame tu numero de ticket, por ejemplo INC0010024, y busco el estado del caso.",
                ct: ct);

            return;
        }

        var ticketNumber = ExtractTicketNumber(text ?? string.Empty);

        if (ticketNumber is null)
        {
            var existingConversation = await _conversationService.GetBySysIdAsync(chatId, ct);
            if (existingConversation is not null)
            {
                // No hay un ticket number valido y hay una existing conversation
                var existingTicket = await _ticketService.GetByIdAsync(existingConversation.TicketId, ct);
                if (existingTicket is not null)
                {
                    await _incomingMessageService.ProcessIncomingAsync(new IncomingMessageRequest(
                            TicketId: existingConversation.TicketId,
                            SysId: existingTicket.SysId,
                            ConversationSysId: chatId,
                            SenderType: "customer",
                            SenderName: chatId,
                            Body: text ?? string.Empty,
                            MessageType: "user_message",
                            SentAt: DateTime.UtcNow
                        ), ct);
                }
            }
            else
            {
                // No hay un ticket number valido y no hay una existing conversation
                await _messageSender.SendToChatAsync(chatId, "No pude identificar un numero de ticket. Enviame algo con INC, por ejemplo INC0010024 o inc10024.", ct: ct);
            }
            return;
        }

        var ticket = await _ticketService.GetByNumberAsync(ticketNumber, ct);

        if (ticket is null)
        {
            // Hay un ticket number valido pero el ticket no existe
            await _messageSender.SendToChatAsync(chatId, "El numero de ticket solicitado no existe. Por favor, verifica el numero y envialo nuevamente.");
            return;
        } else
        {
            // Hay un ticket number valido, y el ticket existe
            // Borramos todas las referencias al chat id
            await _conversationService.ClearSysIdAsync(chatId, ct);

            // Generamos una conversacion nueva y la relacionamos al chatid
            await _incomingMessageService.ProcessIncomingAsync(new IncomingMessageRequest(
             TicketId: ticket.Id,
             SysId: ticket.SysId,
             ConversationSysId: chatId,
             SenderType: "customer",
             SenderName: chatId,
             Body: text ?? string.Empty,
             MessageType: "user_message",
             SentAt: DateTime.UtcNow
         ), ct);
        }
    }

    private static string? ExtractTicketNumber(string text)
    {
        var match = TicketNumberRegex().Match(text);
        return match.Success
            ? string.Concat(match.Value.ToUpperInvariant().Where(char.IsLetterOrDigit))
            : null;
    }

    [GeneratedRegex(@"\bINC[\s-]*\d{4,12}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketNumberRegex();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
