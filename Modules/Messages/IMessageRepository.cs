namespace AgentAI.Modules.Messages;

public interface IMessageRepository
{
    Task<IEnumerable<Message>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Message>> GetByConversationIdAsync(int conversationId, CancellationToken ct = default);
    Task<Message?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Message?> GetBySysIdAsync(string sysId, CancellationToken ct = default);
    Task AddAsync(Message message, CancellationToken ct = default);
    Task UpdateAsync(Message message, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}