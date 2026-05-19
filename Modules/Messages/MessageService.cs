using AgentAI.Modules.Messages.Dto;

namespace AgentAI.Modules.Messages;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _repository;
    private readonly ILogger<MessageService> _logger;

    public MessageService(IMessageRepository repository, ILogger<MessageService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<Message>> GetAllAsync(CancellationToken ct = default)
        => await _repository.GetAllAsync(ct);

    public async Task<IEnumerable<Message>> GetByConversationIdAsync(int conversationId, CancellationToken ct = default)
        => await _repository.GetByConversationIdAsync(conversationId, ct);

    public async Task<Message?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);

    public async Task CreateAsync(CreateMessageRequest req, CancellationToken ct = default)
    {
        var message = new Message
        {
            SysId = req.SysId,
            ConversationId = req.ConversationId,
            SenderType = req.SenderType,
            SenderName = req.SenderName,
            Body = req.Body,
            MessageType = req.MessageType,
            SentAt = req.SentAt,
            LastSyncedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(message, ct);
    }

    public async Task<bool> UpdateAsync(int id, UpdateMessageRequest req, CancellationToken ct = default)
    {
        var message = await _repository.GetByIdAsync(id, ct);
        if (message is null) return false;

        if (req.SenderType is not null) message.SenderType = req.SenderType;
        if (req.SenderName is not null) message.SenderName = req.SenderName;
        if (req.Body is not null) message.Body = req.Body;
        if (req.MessageType is not null) message.MessageType = req.MessageType;
        message.LastSyncedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(message, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        => await _repository.DeleteAsync(id, ct);
}