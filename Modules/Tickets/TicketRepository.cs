using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.Tickets;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _db;
    public TicketRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default)
        => await _db.Tickets.ToListAsync(ct);

    public async Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Ticket?> GetBySysIdAsync(string sysId, CancellationToken ct = default)
        => await _db.Tickets.FirstOrDefaultAsync(t => t.SysId == sysId, ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        _db.Tickets.Update(ticket);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return false;

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}