using System.Text.Json;

namespace AgentAI.Modules.Notifications;

public sealed class FileNotificationPollingStateStore : INotificationPollingStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _statePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileNotificationPollingStateStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration["Notifications:PollingStatePath"];
        _statePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "notification-ticket-polling-state.json")
            : configuredPath;
    }

    public async Task<NotificationPollingState> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_statePath))
                return new NotificationPollingState();

            await using var stream = File.OpenRead(_statePath);
            return await JsonSerializer.DeserializeAsync<NotificationPollingState>(stream, JsonOptions, ct)
                ?? new NotificationPollingState();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(NotificationPollingState state, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var stream = File.Create(_statePath);
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, ct);
        }
        finally
        {
            _lock.Release();
        }
    }
}
