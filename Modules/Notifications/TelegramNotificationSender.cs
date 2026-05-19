namespace AgentAI.Modules.Notifications;

public sealed class TelegramNotificationSender : INotificationSender
{
    private readonly IConfiguration _configuration;
    private readonly ITelegramMessageSender _messageSender;

    public TelegramNotificationSender(
        IConfiguration configuration,
        ITelegramMessageSender messageSender)
    {
        _configuration = configuration;
        _messageSender = messageSender;
    }

    public async Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var chatId = ResolveChatId(message.RecipientEmail);

        if (string.IsNullOrWhiteSpace(chatId))
            return new NotificationResult(false, "Telegram", "Telegram chat id is not configured for the recipient.", message.RecipientEmail);

        return await _messageSender.SendToChatAsync(chatId, BuildText(message), message.RecipientEmail, ct);
    }

    private string? ResolveChatId(string recipientEmail)
        => _configuration[$"Telegram:RecipientChatIds:{recipientEmail}"]
            ?? _configuration["Telegram:DefaultChatId"];

    private static string BuildText(NotificationMessage message)
    {
        var ticket = string.IsNullOrWhiteSpace(message.TicketNumber)
            ? string.Empty
            : $"Ticket: {message.TicketNumber}\n";

        return $"{message.Subject}\n{ticket}\n{message.Body}".Trim();
    }
}
