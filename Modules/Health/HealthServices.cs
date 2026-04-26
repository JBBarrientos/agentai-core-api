using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.Health;

public class HealthService
{
    private readonly AppDbContext _db;

    public HealthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsDatabaseReadyAsync()
    {
        return await _db.Database.CanConnectAsync();
    }
}