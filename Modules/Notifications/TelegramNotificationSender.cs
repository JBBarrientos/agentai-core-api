using System.Net.Http.Json;

namespace AgentAI.Modules.Notifications;

public sealed class TelegramNotificationSender : INotificationSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramNotificationSender> _logger;

    public TelegramNotificationSender(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TelegramNotificationSender> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var botToken = _configuration["Telegram:BotToken"];
        var chatId = ResolveChatId(message.RecipientEmail);

        if (string.IsNullOrWhiteSpace(botToken))
            return new NotificationResult(false, "Telegram", "Telegram:BotToken is not configured.", message.RecipientEmail);

        if (string.IsNullOrWhiteSpace(chatId))
            return new NotificationResult(false, "Telegram", "Telegram chat id is not configured for the recipient.", message.RecipientEmail);

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{botToken}/sendMessage",
            new
            {
                chat_id = chatId,
                text = BuildText(message),
                disable_web_page_preview = true
            },
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Telegram notification failed. Status={StatusCode} Body={Body}", response.StatusCode, body);
            return new NotificationResult(false, "Telegram", $"Telegram returned {response.StatusCode}: {body}", message.RecipientEmail);
        }

        return new NotificationResult(true, "Telegram", "Notification sent.", message.RecipientEmail);
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
