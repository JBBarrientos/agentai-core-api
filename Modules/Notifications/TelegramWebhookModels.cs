using System.Text.Json.Serialization;

namespace AgentAI.Modules.Notifications;

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")]
    long UpdateId,
    [property: JsonPropertyName("message")]
    TelegramMessage? Message
);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")]
    long MessageId,
    [property: JsonPropertyName("chat")]
    TelegramChat Chat,
    [property: JsonPropertyName("from")]
    TelegramUser? From,
    [property: JsonPropertyName("text")]
    string? Text
);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("type")]
    string? Type,
    [property: JsonPropertyName("username")]
    string? Username,
    [property: JsonPropertyName("first_name")]
    string? FirstName,
    [property: JsonPropertyName("last_name")]
    string? LastName
);

public sealed record TelegramUser(
    [property: JsonPropertyName("id")]
    long Id,
    [property: JsonPropertyName("is_bot")]
    bool IsBot,
    [property: JsonPropertyName("username")]
    string? Username,
    [property: JsonPropertyName("first_name")]
    string? FirstName,
    [property: JsonPropertyName("last_name")]
    string? LastName
);
