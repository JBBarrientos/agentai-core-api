using System.Net;

namespace AgentAI.Modules.ServiceNow;

public sealed class ServiceNowRetryHandler : DelegatingHandler
{
    private static readonly HashSet<HttpStatusCode> TransientStatusCodes =
    [
        HttpStatusCode.RequestTimeout,
        (HttpStatusCode)429,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    ];

    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceNowRetryHandler> _logger;

    public ServiceNowRetryHandler(IConfiguration configuration, ILogger<ServiceNowRetryHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var retryCount = Math.Max(GetIntSetting("RetryCount", 3), 0);
        var baseDelaySeconds = Math.Max(GetIntSetting("RetryBaseDelaySeconds", 2), 1);
        var maxAttempts = retryCount + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var attemptRequest = await CloneRequestAsync(request, cancellationToken);

            try
            {
                var response = await base.SendAsync(attemptRequest, cancellationToken);
                if (!ShouldRetry(response.StatusCode) || attempt == maxAttempts)
                    return response;

                _logger.LogWarning(
                    "ServiceNow request returned {StatusCode}. Retrying attempt {NextAttempt}/{MaxAttempts}.",
                    (int)response.StatusCode,
                    attempt + 1,
                    maxAttempts);

                response.Dispose();
            }
            catch (Exception ex) when (IsTransientException(ex, cancellationToken) && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "ServiceNow request failed transiently. Retrying attempt {NextAttempt}/{MaxAttempts}.",
                    attempt + 1,
                    maxAttempts);
            }

            await Task.Delay(GetDelay(attempt, baseDelaySeconds), cancellationToken);
        }

        throw new InvalidOperationException("ServiceNow retry handler reached an invalid state.");
    }

    private int GetIntSetting(string key, int defaultValue)
        => int.TryParse(_configuration[$"ServiceNow:{key}"], out var value)
            ? value
            : defaultValue;

    private static bool ShouldRetry(HttpStatusCode statusCode)
        => TransientStatusCodes.Contains(statusCode);

    private static bool IsTransientException(Exception ex, CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested &&
           (ex is HttpRequestException or TimeoutException or TaskCanceledException);

    private static TimeSpan GetDelay(int failedAttempt, int baseDelaySeconds)
    {
        var exponentialDelaySeconds = baseDelaySeconds * Math.Pow(2, failedAttempt - 1);
        return TimeSpan.FromSeconds(Math.Min(exponentialDelaySeconds, 30));
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
