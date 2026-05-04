using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentAI.Modules.ServiceNow;

public interface IServiceNowConnector
{
    Task<IReadOnlyList<ServiceNowIncident>> GetIncidentsAsync(int limit = 20, string? query = null, CancellationToken ct = default);
    Task<ServiceNowIncident?> GetIncidentAsync(string sysId, CancellationToken ct = default);
}

public sealed class ServiceNowConnector : IServiceNowConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public ServiceNowConnector(HttpClient client, IConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    private string? GetSetting(string key)
        => _configuration[$"ServiceNow:{key}"]
            ?? Environment.GetEnvironmentVariable($"SERVICE_NOW_{ToSnakeCase(key)}")
            ?? Environment.GetEnvironmentVariable($"SERVICE_NOW_{key.ToUpperInvariant()}");

    private void EnsureConfigured()
    {
        var baseUrl = GetSetting("BaseUrl");

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ServiceNow:BaseUrl or SERVICE_NOW_BASE_URL is not set.");

        if (!string.IsNullOrWhiteSpace(GetSetting("AccessToken")))
            return;

        if (string.IsNullOrWhiteSpace(GetSetting("ClientId")) ||
            string.IsNullOrWhiteSpace(GetSetting("ClientSecret")) ||
            string.IsNullOrWhiteSpace(GetSetting("Username")) ||
            string.IsNullOrWhiteSpace(GetSetting("Password")))
        {
            throw new InvalidOperationException("ServiceNow OAuth settings are incomplete. Set ServiceNow:ClientId, ClientSecret, Username and Password, or provide ServiceNow:AccessToken.");
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var configuredToken = GetSetting("AccessToken");
        if (!string.IsNullOrWhiteSpace(configuredToken))
            return configuredToken;

        if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _accessToken;

        var baseUrl = GetSetting("BaseUrl")!.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/oauth_token.do");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = GetSetting("ClientId")!,
            ["client_secret"] = GetSetting("ClientSecret")!,
            ["username"] = GetSetting("Username")!,
            ["password"] = GetSetting("Password")!
        });

        using var response = await _client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ServiceNow OAuth returned {response.StatusCode}: {body}");

        var tokenResponse = JsonSerializer.Deserialize<ServiceNowTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("ServiceNow OAuth response is empty.");

        _accessToken = tokenResponse.AccessToken;
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(tokenResponse.ExpiresIn - 30, 60));
        return _accessToken;
    }

    public async Task<IReadOnlyList<ServiceNowIncident>> GetIncidentsAsync(int limit = 20, string? query = null, CancellationToken ct = default)
    {
        EnsureConfigured();

        var baseUrl = GetSetting("BaseUrl")!.TrimEnd('/');
        var table = string.IsNullOrWhiteSpace(GetSetting("IncidentTable")) ? "incident" : GetSetting("IncidentTable")!;

        var url = new StringBuilder();
        url.Append(baseUrl);
        url.Append("/api/now/table/");
        url.Append(Uri.EscapeDataString(table));
        url.Append("?sysparm_fields=sys_id,number,short_description,description,comments,comments_and_work_notes,state,urgency,opened_at,sys_updated_on");
        url.Append("&sysparm_query=");
        url.Append(Uri.EscapeDataString(string.IsNullOrWhiteSpace(query) ? "ORDERBYDESCsys_updated_on" : query));
        url.Append("&sysparm_limit=");
        url.Append(limit.ToString(CultureInfo.InvariantCulture));

        var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(ct));

        var res = await _client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"ServiceNow returned {res.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("result", out var result))
            return Array.Empty<ServiceNowIncident>();

        var list = new List<ServiceNowIncident>();
        foreach (var item in result.EnumerateArray())
        {
            var sysId = item.GetProperty("sys_id").GetString() ?? string.Empty;
            var number = item.GetProperty("number").GetString() ?? string.Empty;
            var shortDesc = item.TryGetProperty("short_description", out var sd) ? sd.GetString() ?? string.Empty : string.Empty;
            var desc = GetDescription(item);
            if (string.IsNullOrWhiteSpace(desc))
                desc = await GetLatestCustomerCommentAsync(sysId, ct);
            var state = item.TryGetProperty("state", out var s) && int.TryParse(s.GetString(), out var si) ? si : 0;
            var urgency = item.TryGetProperty("urgency", out var u) && int.TryParse(u.GetString(), out var ui) ? ui : 0;
            var openedAt = item.TryGetProperty("opened_at", out var oa) && DateTime.TryParse(oa.GetString(), out var oaDt) ? DateTime.SpecifyKind(oaDt, DateTimeKind.Utc) : (DateTime?)null;
            var updatedAt = item.TryGetProperty("sys_updated_on", out var ua) && DateTime.TryParse(ua.GetString(), out var uaDt) ? DateTime.SpecifyKind(uaDt, DateTimeKind.Utc) : (DateTime?)null;

            list.Add(new ServiceNowIncident(sysId, number, shortDesc, desc, state, MapStateLabel(state), urgency, MapUrgencyLabel(urgency), openedAt, updatedAt));
        }

        return list;
    }

    public async Task<ServiceNowIncident?> GetIncidentAsync(string sysId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sysId)) throw new ArgumentException("sysId is required", nameof(sysId));
        EnsureConfigured();

        var baseUrl = GetSetting("BaseUrl")!.TrimEnd('/');
        var table = string.IsNullOrWhiteSpace(GetSetting("IncidentTable")) ? "incident" : GetSetting("IncidentTable")!;

        var url = new StringBuilder();
        url.Append(baseUrl);
        url.Append("/api/now/table/");
        url.Append(Uri.EscapeDataString(table));
        url.Append("?sysparm_fields=sys_id,number,short_description,description,comments,comments_and_work_notes,state,urgency,opened_at,sys_updated_on");
        url.Append("&sysparm_query=");
        url.Append(Uri.EscapeDataString($"sys_id={sysId}"));

        var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(ct));

        var res = await _client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"ServiceNow returned {res.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("result", out var result) || result.GetArrayLength() == 0)
            return null;

        var item = result[0];
        var number = item.GetProperty("number").GetString() ?? string.Empty;
        var shortDesc = item.TryGetProperty("short_description", out var sd) ? sd.GetString() ?? string.Empty : string.Empty;
        var desc = GetDescription(item);
        if (string.IsNullOrWhiteSpace(desc))
            desc = await GetLatestCustomerCommentAsync(sysId, ct);
        var state = item.TryGetProperty("state", out var s) && int.TryParse(s.GetString(), out var si) ? si : 0;
        var urgency = item.TryGetProperty("urgency", out var u) && int.TryParse(u.GetString(), out var ui) ? ui : 0;
        var openedAt = item.TryGetProperty("opened_at", out var oa) && DateTime.TryParse(oa.GetString(), out var oaDt) ? DateTime.SpecifyKind(oaDt, DateTimeKind.Utc) : (DateTime?)null;
        var updatedAt = item.TryGetProperty("sys_updated_on", out var ua) && DateTime.TryParse(ua.GetString(), out var uaDt) ? DateTime.SpecifyKind(uaDt, DateTimeKind.Utc) : (DateTime?)null;

        return new ServiceNowIncident(sysId, number, shortDesc, desc, state, MapStateLabel(state), urgency, MapUrgencyLabel(urgency), openedAt, updatedAt);
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0)
                builder.Append('_');
            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }

    private static string GetDescription(JsonElement item)
    {
        var description = GetString(item, "description");
        if (!string.IsNullOrWhiteSpace(description))
            return description;

        var comments = GetString(item, "comments");
        if (!string.IsNullOrWhiteSpace(comments))
            return comments;

        return GetString(item, "comments_and_work_notes");
    }

    private static string GetString(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var property) ? property.GetString() ?? string.Empty : string.Empty;

    private async Task<string> GetLatestCustomerCommentAsync(string sysId, CancellationToken ct)
    {
        var baseUrl = GetSetting("BaseUrl")!.TrimEnd('/');
        var url = new StringBuilder();
        url.Append(baseUrl);
        url.Append("/api/now/table/sys_journal_field");
        url.Append("?sysparm_fields=value,sys_created_on,sys_created_by");
        url.Append("&sysparm_query=");
        url.Append(Uri.EscapeDataString($"element_id={sysId}^element=comments^ORDERBYDESCsys_created_on"));
        url.Append("&sysparm_limit=1");

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(ct));

        using var res = await _client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"ServiceNow journal returned {res.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("result", out var result) || result.GetArrayLength() == 0)
            return string.Empty;

        return GetString(result[0], "value");
    }

    private static string MapStateLabel(int state) => state switch
    {
        1 => "New",
        2 => "In Progress",
        3 => "On Hold",
        4 => "Resolved",
        5 => "Closed",
        6 => "Canceled",
        _ => "Unknown"
    };

    private static string MapUrgencyLabel(int urgency) => urgency switch
    {
        1 => "High",
        2 => "Medium",
        3 => "Low",
        _ => "Unknown"
    };
}
