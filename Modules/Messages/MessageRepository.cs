using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.Messages;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _db;
    public MessageRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Message>> GetAllAsync(CancellationToken ct = default)
        => await _db.Messages.ToListAsync(ct);

    public async Task<IEnumerable<Message>> GetByConversationIdAsync(int conversationId, CancellationToken ct = default)
        => await _db.Messages.Where(m => m.ConversationId == conversationId).ToListAsync(ct);

    public async Task<Message?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Messages.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Message?> GetBySysIdAsync(string sysId, CancellationToken ct = default)
        => await _db.Messages.FirstOrDefaultAsync(m => m.SysId == sysId, ct);

    public async Task AddAsync(Message message, CancellationToken ct = default)
    {
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Message message, CancellationToken ct = default)
    {
        _db.Messages.Update(message);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (message is null) return false;
        _db.Messages.Remove(message);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}