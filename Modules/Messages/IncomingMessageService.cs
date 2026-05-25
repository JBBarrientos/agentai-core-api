using System.Text.Json;
using AgentAI.Modules.Conversations;
using AgentAI.Modules.Messages.Dto;
using AgentAI.Modules.Notifications;
using AgentAI.Modules.Queue;

namespace AgentAI.Modules.Messages;

public class IncomingMessageService : IIncomingMessageService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IQueueService _queueService;
    private readonly ITelegramMessageSender _messageSender;
    private readonly ILogger<IncomingMessageService> _logger;

    public IncomingMessageService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        [FromKeyedServices("inbound")] IQueueService queueService,
        ITelegramMessageSender messageSender,
        ILogger<IncomingMessageService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _queueService = queueService;
        _messageSender = messageSender;
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

    public async Task ProcessOutboundAsync(OutboundMessagePayload payload, CancellationToken ct = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(payload.ConversationId, ct);
        if (conversation is null)
        {
            _logger.LogWarning("ProcessOutboundAsync: conversation {ConversationId} not found.", payload.ConversationId);
            return;
        }

        await PersistCoreAsync(new IncomingMessageRequest(
            TicketId: conversation.TicketId,
            SysId: Guid.NewGuid().ToString(), // TODO: replace with real message SysId if available
            ConversationSysId: conversation.SysId,
            SenderType: "bot",
            SenderName: "Agent",
            Body: payload.Body,
            MessageType: payload.MessageType,
            SentAt: DateTime.UtcNow
        ), ct);

        await _messageSender.SendToChatAsync(conversation.SysId, payload.Body, ct: ct);
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
        var existing = await _conversationRepository.GetBySysIdAsync(req.ConversationSysId, ct);
        if (existing is not null)
            return (existing, false);


        var conversation = new Conversation
        {
            TicketId = req.TicketId,
            SysId = req.ConversationSysId,
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