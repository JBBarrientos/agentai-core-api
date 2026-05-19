using AgentAI.Modules.Conversations.Dto;
using AgentAI.Modules.Messages.Dto;

namespace AgentAI.Modules.Messages;

public interface IMessageService
{
    Task<IEnumerable<Message>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Message>> GetByConversationIdAsync(int conversationId, CancellationToken ct = default);
    Task<Message?> GetByIdAsync(int id, CancellationToken ct = default);
    Task CreateAsync(CreateMessageRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateMessageRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}