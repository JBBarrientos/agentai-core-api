namespace AgentAI.Modules.Queue;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

public class OutboundQueueWorker : BackgroundService
{
    private readonly IQueueService _outbound;
    private readonly ActionDispatcher _dispatcher;
    private readonly ILogger<OutboundQueueWorker> _logger;

    public OutboundQueueWorker(
        [FromKeyedServices("outbound")] IQueueService outbound,
        ActionDispatcher dispatcher,
        ILogger<OutboundQueueWorker> logger)
    {
        _outbound = outbound;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbound queue worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _outbound.ReceiveMessagesAsync(10, stoppingToken);
                foreach (var message in messages)
                    await ProcessMessageAsync(message, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error polling outbound queue");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(QueueMessage message, CancellationToken ct)
    {
        try
        {
            var outbound = JsonSerializer.Deserialize<OutboundMessage>(message.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Could not deserialize message");

            await _dispatcher.DispatchAsync(outbound);
            await _outbound.DeleteMessageAsync(message.ReceiptHandle, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process outbound message {MessageId}", message.MessageId);
        }
    }
}