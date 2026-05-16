using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.Notifications;

public sealed class NotificationTicketPollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationTicketPollingWorker> _logger;
    private bool _isStartup = true;
    private NotificationPollingState _state = new();

    public NotificationTicketPollingWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NotificationTicketPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Notifications:PollingEnabled", false))
        {
            _logger.LogInformation("Notification ticket polling is disabled.");
            return;
        }

        var intervalSeconds = Math.Max(_configuration.GetValue("Notifications:PollingIntervalSeconds", 60), 10);
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        using (var scope = _scopeFactory.CreateScope())
        {
            var stateStore = scope.ServiceProvider.GetRequiredService<INotificationPollingStateStore>();
            _state = await stateStore.LoadAsync(stoppingToken);
        }

        _logger.LogInformation(
            "Notification ticket polling started. Interval: {IntervalSeconds} seconds. Last processed: {LastProcessedTicketNumber} at {LastProcessedOpenedAtUtc}.",
            intervalSeconds,
            string.IsNullOrWhiteSpace(_state.LastProcessedTicketNumber) ? "(none)" : _state.LastProcessedTicketNumber,
            _state.LastProcessedOpenedAtUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification ticket polling failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var serviceNow = scope.ServiceProvider.GetRequiredService<IServiceNowConnector>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var stateStore = scope.ServiceProvider.GetRequiredService<INotificationPollingStateStore>();

        var limit = Math.Max(_configuration.GetValue("Notifications:PollingLimit", 20), 1);
        var maxPages = Math.Max(_configuration.GetValue("Notifications:PollingMaxPages", 5), 1);
        var query = BuildPollingQuery();

        var notifyExistingOnStartup = _configuration.GetValue("Notifications:NotifyExistingOnStartup", false);
        var hasPersistedState = _state.LastProcessedOpenedAtUtc is not null || _state.ProcessedTicketSysIds.Count > 0;
        var tickets = await serviceNow.GetIncidentsPagedAsync(limit, maxPages, query, ct);
        var knownOnStartupCount = 0;
        var notifiedCount = 0;

        foreach (var ticket in tickets.OrderBy(t => t.OpenedAt ?? DateTime.MaxValue))
        {
            if (string.IsNullOrWhiteSpace(ticket.SysId))
                continue;

            if (_state.IsProcessed(ticket.SysId))
                continue;

            if (_isStartup && !hasPersistedState && !notifyExistingOnStartup)
            {
                _state.MarkProcessed(ticket.SysId, ticket.Number, ticket.OpenedAt);
                knownOnStartupCount++;
                continue;
            }

            var result = await notifications.NotifyReviewStartedAsync(ticket.SysId, ct);
            _state.MarkProcessed(ticket.SysId, ticket.Number, ticket.OpenedAt);
            notifiedCount++;
            _logger.LogInformation(
                "Review-started notification for ticket {TicketNumber}. Sent={Sent}. Provider={Provider}. Message={Message}",
                ticket.Number,
                result.Sent,
                result.Provider,
                result.Message);
        }

        if (knownOnStartupCount > 0)
            _logger.LogInformation("Notification polling marked {Count} existing tickets as already known on startup.", knownOnStartupCount);

        if (tickets.Count > 0 || notifiedCount > 0)
            _logger.LogInformation("Notification polling checked {CheckedCount} tickets and sent {NotifiedCount} notifications.", tickets.Count, notifiedCount);

        if (tickets.Count > 0)
            await stateStore.SaveAsync(_state, ct);

        _isStartup = false;
    }

    private string BuildPollingQuery()
    {
        var configuredQuery = _configuration["Notifications:PollingQuery"];
        if (!string.IsNullOrWhiteSpace(configuredQuery))
            return configuredQuery;

        var startupLookbackMinutes = Math.Max(_configuration.GetValue("Notifications:PollingStartupLookbackMinutes", 2), 0);
        var overlapMinutes = Math.Max(_configuration.GetValue("Notifications:PollingOverlapMinutes", 1), 0);
        var from = _state.LastProcessedOpenedAtUtc?.AddMinutes(-overlapMinutes)
            ?? DateTime.UtcNow.AddMinutes(-startupLookbackMinutes);

        return $"incident_state=1^sys_created_on>={from:yyyy-MM-dd HH:mm:ss}^ORDERBYsys_created_on";
    }
}
