using System.Text.Json;
using AgentAI.Modules.Conversations;
using AgentAI.Modules.Messages.Dto;
using AgentAI.Modules.Queue;

namespace AgentAI.Modules.Messages;

public class IncomingMessageService : IIncomingMessageService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IQueueService _queueService;
    private readonly ILogger<IncomingMessageService> _logger;

    public IncomingMessageService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        [FromKeyedServices("inbound")] IQueueService queueService,
        ILogger<IncomingMessageService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _queueService = queueService;
        _logger = logger;
    }

    public async Task<IncomingMessageResponse> ProcessIncomingAsync(
        IncomingMessageRequest req,
        CancellationToken ct = default)
    {
        var (message, conversation, conversationCreated) = await PersistCoreAsync(req, ct);

        await _queueService.SendMessageAsync(
            JsonSerializer.Serialize(new InboundMessage(
                TicketId: conversation.TicketId.ToString(),
                CorrelationId: Guid.NewGuid().ToString(),
                CustomerId: conversation.SysId,
                Action: "user_message",
                Payload: JsonSerializer.Serialize(new IncomingMessagePayload(
                    MessageId: message.Id,
                    ConversationId: conversation.Id,
                    ConversationSysId: conversation.SysId,
                    SysId: message.SysId,
                    SenderType: message.SenderType,
                    SenderName: message.SenderName,
                    Body: message.Body,
                    MessageType: message.MessageType,
                    SentAt: message.SentAt
                ))
            )),
            ct);

        return BuildResponse(message, conversation, conversationCreated);
    }

    public async Task<IncomingMessageResponse> ProcessOutboundAsync(
        IncomingMessageRequest req,
        CancellationToken ct = default)
    {
        var (message, conversation, conversationCreated) = await PersistCoreAsync(req, ct);
        return BuildResponse(message, conversation, conversationCreated);
    }

    private async Task<(Message message, Conversation conversation, bool conversationCreated)> PersistCoreAsync(
        IncomingMessageRequest req,
        CancellationToken ct)
    {
        var (conversation, conversationCreated) = await ResolveConversationAsync(req, ct);

        var message = new Message
        {
            SysId = req.SysId,
            ConversationId = conversation.Id,
            SenderType = req.SenderType,
            SenderName = req.SenderName,
            Body = req.Body,
            MessageType = req.MessageType,
            SentAt = req.SentAt,
            LastSyncedAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message, ct);

        _logger.LogInformation(
            "Persisted message {MessageId} for conversation {ConversationId} (created={Created})",
            message.Id, conversation.Id, conversationCreated);

        return (message, conversation, conversationCreated);
    }

    private async Task<(Conversation conversation, bool created)> ResolveConversationAsync(
        IncomingMessageRequest req,
        CancellationToken ct)
    {
        var existing = await _conversationRepository.GetByIdAsync(req.ConversationId, ct);
        if (existing is not null)
            return (existing, false);

        if (req.TicketId is null)
            throw new InvalidOperationException(
                $"No conversation found for Id '{req.ConversationId}' " +
                "and no TicketId was provided to create one.");

        var conversation = new Conversation
        {
            TicketId = req.TicketId.Value,
            Channel = "telegram",
            Status = "active",
            StartedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow
        };

        await _conversationRepository.AddAsync(conversation, ct);
        return (conversation, true);
    }

    private static IncomingMessageResponse BuildResponse(
        Message message,
        Conversation conversation,
        bool conversationCreated) => new(
            MessageId: message.Id,
            ConversationId: conversation.Id,
            ConversationSysId: conversation.SysId,
            ConversationCreated: conversationCreated,
            SysId: message.SysId,
            SenderType: message.SenderType,
            SenderName: message.SenderName,
            Body: message.Body,
            MessageType: message.MessageType,
            SentAt: message.SentAt,
            LastSyncedAt: message.LastSyncedAt
        );
}