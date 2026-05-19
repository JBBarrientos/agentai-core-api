namespace AgentAI.Modules.Conversations;

public interface IConversationRepository
{
    Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Conversation>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default);
    Task<Conversation?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Conversation?> GetBySysIdAsync(string sysId, CancellationToken ct = default);
    Task AddAsync(Conversation conversation, CancellationToken ct = default);
    Task UpdateAsync(Conversation conversation, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}