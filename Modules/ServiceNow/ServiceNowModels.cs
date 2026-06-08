using System.Text.Json.Serialization;

namespace AgentAI.Modules.ServiceNow;

public sealed record ServiceNowIncident(
    string SysId,
    string Number,
    string Title,
    string Description,
    int State,
    string StateLabel,
    int Priority,
    string PriorityLabel,
    string CreatedByName,
    string CreatedByEmail,
    string AssignmentGroup,
    DateTime? OpenedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt
);

public sealed record ServiceNowUser(
    string SysId,
    string Name,
    string Email
);

public sealed record ServiceNowGroup(
    string SysId,
    string Name
);

public sealed record ServiceNowTokenResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("refresh_token")]
    string RefreshToken,
    [property: JsonPropertyName("scope")]
    string Scope,
    [property: JsonPropertyName("token_type")]
    string TokenType,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn
);
