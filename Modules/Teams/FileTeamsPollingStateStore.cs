using System.Text.Json;

namespace AgentAI.Modules.Teams;

public sealed class FileTeamsPollingStateStore : ITeamsPollingStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _statePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileTeamsPollingStateStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration["Teams:PollingStatePath"];
        _statePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "teams-ticket-polling-state.json")
            : configuredPath;
    }

    public async Task<TeamsPollingState> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_statePath))
                return new TeamsPollingState();

            await using var stream = File.OpenRead(_statePath);
            return await JsonSerializer.DeserializeAsync<TeamsPollingState>(stream, JsonOptions, ct)
                ?? new TeamsPollingState();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(TeamsPollingState state, CancellationToken ct = default)
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
