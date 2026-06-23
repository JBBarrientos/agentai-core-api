using AgentAI.Modules.Conversations.Dto;

namespace AgentAI.Modules.Conversations;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _repository;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(IConversationRepository repository, ILogger<ConversationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken ct = default)
        => await _repository.GetAllAsync(ct);

    public async Task<IEnumerable<Conversation>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default)
        => await _repository.GetByTicketIdAsync(ticketId, ct);
    public async Task<Conversation?> GetBySysIdAsync(string sysId, CancellationToken ct = default)
        => await _repository.GetBySysIdAsync(sysId, ct);
    public async Task<IEnumerable<Conversation>> GetAllBySysIdAsync(string sysId, CancellationToken ct = default) 
        => await _repository.GetAllBySysIdAsync(sysId, ct);
    public async Task<Conversation?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);
    public async Task ClearSysIdAsync(string sysId, CancellationToken ct)
    {
        var conversations = await _repository.GetAllBySysIdAsync(sysId, ct);

        foreach (var conversation in conversations)
        {
            conversation.SysId = string.Empty;
            await _repository.UpdateAsync(conversation, ct);
        }
    }
    public async Task CreateAsync(CreateConversationRequest req, CancellationToken ct = default)
    {
        var conversation = new Conversation
        {
            SysId = req.SysId,
            TicketId = req.TicketId,
            Channel = req.Channel,
            Status = req.Status,
            StartedAt = req.StartedAt,
            LastSyncedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(conversation, ct);
    }

    public async Task<bool> UpdateAsync(int id, UpdateConversationRequest req, CancellationToken ct = default)
    {
        var conversation = await _repository.GetByIdAsync(id, ct);
        if (conversation is null) return false;

        if (req.Channel is not null) conversation.Channel = req.Channel;
        if (req.Status is not null) conversation.Status = req.Status;
        if (req.EndedAt is not null) conversation.EndedAt = req.EndedAt;
        conversation.LastSyncedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(conversation, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        => await _repository.DeleteAsync(id, ct);
}