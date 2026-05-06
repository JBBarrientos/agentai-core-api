using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentAI.Modules.Teams;

public sealed class MicrosoftGraphClient : IMicrosoftGraphClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MicrosoftGraphClient> _logger;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public MicrosoftGraphClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MicrosoftGraphClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TeamsUser?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var token = await GetAccessTokenAsync(ct);
        var encodedEmail = Uri.EscapeDataString(email);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/users/{encodedEmail}?$select=id,displayName,mail,userPrincipalName");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft Graph user lookup returned {response.StatusCode}: {body}");

        var graphUser = JsonSerializer.Deserialize<GraphUserResponse>(body, JsonOptions);
        if (graphUser is null)
            return null;

        var resolvedEmail = string.IsNullOrWhiteSpace(graphUser.Mail)
            ? graphUser.UserPrincipalName
            : graphUser.Mail;

        return new TeamsUser(
            graphUser.Id ?? string.Empty,
            graphUser.DisplayName ?? string.Empty,
            resolvedEmail ?? email);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _accessToken;

        var tenantId = GetSetting("TenantId");
        var clientId = GetSetting("ClientId");
        var clientSecret = GetSetting("ClientSecret");

        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("MicrosoftGraph settings are incomplete. Set MicrosoftGraph:TenantId, ClientId and ClientSecret.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft Graph OAuth returned {response.StatusCode}: {body}");

        var token = JsonSerializer.Deserialize<GraphTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Microsoft Graph OAuth response is empty.");

        _accessToken = token.AccessToken;
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresIn - 30, 60));

        _logger.LogInformation("Microsoft Graph access token acquired. Expires in {ExpiresIn} seconds.", token.ExpiresIn);
        return _accessToken;
    }

    private string? GetSetting(string key)
        => _configuration[$"MicrosoftGraph:{key}"]
            ?? Environment.GetEnvironmentVariable($"MICROSOFT_GRAPH_{ToSnakeCase(key)}")
            ?? Environment.GetEnvironmentVariable($"MicrosoftGraph__{key}");

    private static string ToSnakeCase(string value)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0)
                builder.Append('_');
            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }

    private sealed record GraphTokenResponse(
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("access_token")] string AccessToken
    );

    private sealed record GraphUserResponse(
        string? Id,
        string? DisplayName,
        string? Mail,
        string? UserPrincipalName
    );
}
