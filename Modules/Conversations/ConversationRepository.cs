using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.Conversations;

public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _db;
    public ConversationRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken ct = default)
        => await _db.Conversations.ToListAsync(ct);

    public async Task<IEnumerable<Conversation>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default)
        => await _db.Conversations.Where(c => c.TicketId == ticketId).ToListAsync(ct);

    public async Task<Conversation?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Conversation?> GetBySysIdAsync(string sysId, CancellationToken ct = default)
        => await _db.Conversations.FirstOrDefaultAsync(c => c.SysId == sysId, ct);

    public async Task AddAsync(Conversation conversation, CancellationToken ct = default)
    {
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken ct = default)
    {
        _db.Conversations.Update(conversation);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conversation is null) return false;
        _db.Conversations.Remove(conversation);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}