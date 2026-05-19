using Amazon.SQS;
using Amazon.SQS.Model;

namespace AgentAI.Modules.Queue;


public class SqsMessageQueue : IQueueService
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;

    public SqsMessageQueue(IAmazonSQS sqs, string queueUrl)
    {
        _sqs = sqs;
        _queueUrl = queueUrl;
    }

    public async Task<IReadOnlyList<QueueMessage>> ReceiveMessagesAsync(int maxMessages, CancellationToken ct)
    {
        var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = 20
        }, ct);

        return (response.Messages ?? [])
            .Select(m => new QueueMessage(m.MessageId, m.Body, m.ReceiptHandle))
            .ToList();
    }

    public async Task SendMessageAsync(string body, CancellationToken ct)
    {
        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = body
        }, ct);
    }

    public async Task DeleteMessageAsync(string receiptHandle, CancellationToken ct)
    {
        await _sqs.DeleteMessageAsync(_queueUrl, receiptHandle, ct);
    }
}