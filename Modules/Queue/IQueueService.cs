namespace AgentAI.Modules.Queue;
public interface IQueueService
{
    Task<IReadOnlyList<QueueMessage>> ReceiveMessagesAsync(int maxMessages, CancellationToken ct);
    Task SendMessageAsync(string body, CancellationToken ct);
    Task DeleteMessageAsync(string receiptHandle, CancellationToken ct);
}

public record QueueMessage(string MessageId, string Body, string ReceiptHandle);