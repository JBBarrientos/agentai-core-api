using System.Net.Http.Json;

namespace AgentAI.Modules.Notifications;

public sealed class TelegramMessageSender : ITelegramMessageSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramMessageSender> _logger;

    public TelegramMessageSender(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TelegramMessageSender> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<NotificationResult> SendToChatAsync(string chatId, string text, string? recipientEmail = null, CancellationToken ct = default)
    {
        var botToken = _configuration["Telegram:BotToken"];

        if (string.IsNullOrWhiteSpace(botToken))
            return new NotificationResult(false, "Telegram", "Telegram:BotToken is not configured.", recipientEmail);

        if (string.IsNullOrWhiteSpace(chatId))
            return new NotificationResult(false, "Telegram", "Telegram chat id is required.", recipientEmail);

        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{botToken}/sendMessage",
            new
            {
                chat_id = chatId,
                text,
                disable_web_page_preview = true
            },
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Telegram message failed. Status={StatusCode} Body={Body}", response.StatusCode, body);
            return new NotificationResult(false, "Telegram", $"Telegram returned {response.StatusCode}: {body}", recipientEmail);
        }

        return new NotificationResult(true, "Telegram", "Notification sent.", recipientEmail);
    }
}
