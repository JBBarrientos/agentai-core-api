using AgentAI.Modules.Tickets;

namespace AgentAI.Modules.ServiceNow;

public sealed class ServiceNowSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceNowSyncWorker> _logger;

    public ServiceNowSyncWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<ServiceNowSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("ServiceNow:SyncEnabled", false))
        {
            _logger.LogInformation("ServiceNow background sync is disabled.");
            return;
        }

        var intervalMinutes = Math.Max(_configuration.GetValue("ServiceNow:SyncIntervalMinutes", 5), 1);
        var limit = Math.Max(_configuration.GetValue("ServiceNow:SyncLimit", 100), 1);
        var query = _configuration["ServiceNow:SyncQuery"] ?? "ORDERBYDESCsys_updated_on";

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        await SyncAsync(limit, query, stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await SyncAsync(limit, query, stoppingToken);
    }

    private async Task SyncAsync(int limit, string query, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var ticketService = scope.ServiceProvider.GetRequiredService<ITicketService>();

            await ticketService.SyncFromServiceNowAsync(limit, query, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceNow background sync failed.");
        }
    }
}
