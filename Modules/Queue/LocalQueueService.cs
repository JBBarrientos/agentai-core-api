
namespace AgentAI.Modules.Queue;

public class LocalQueueService : IQueueService
{
    private readonly ILogger<LocalQueueService> _logger;

    public LocalQueueService(ILogger<LocalQueueService> logger) => _logger = logger;

    public Task DeleteMessageAsync(string receiptHandle, CancellationToken ct)
    {
        _logger.LogInformation("[LOCAL QUEUE] Message not deleted");
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<QueueMessage>> ReceiveMessagesAsync(int maxMessages, CancellationToken ct)
    {
        _logger.LogInformation("[LOCAL QUEUE] Message not received");
        throw new NotImplementedException();
    }

    public Task SendMessageAsync(string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[LOCAL QUEUE] Message not sent: {Body}", body);
        return Task.CompletedTask;
    }

}