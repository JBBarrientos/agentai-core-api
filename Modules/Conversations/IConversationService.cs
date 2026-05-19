using AgentAI.Modules.Conversations.Dto;

namespace AgentAI.Modules.Conversations;

public interface IConversationService
{
    Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Conversation>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default);
    Task<Conversation?> GetByIdAsync(int id, CancellationToken ct = default);
    Task CreateAsync(CreateConversationRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateConversationRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}