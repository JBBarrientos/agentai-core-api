using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.AgentSteps;

public class AgentStepRepository : IAgentStepRepository
{
    private readonly AppDbContext _db;
    public AgentStepRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<AgentStep>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default)
        => await _db.AgentSteps
            .Where(s => s.AgentRunId == agentRunId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<AgentStep?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.AgentSteps.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(AgentStep step, CancellationToken ct = default)
    {
        _db.AgentSteps.Add(step);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AgentStep step, CancellationToken ct = default)
    {
        _db.AgentSteps.Update(step);
        await _db.SaveChangesAsync(ct);
    }
}