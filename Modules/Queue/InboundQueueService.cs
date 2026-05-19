using System.Text.Json;

namespace AgentAI.Modules.Queue;
public class InboundQueueService
{
    private readonly IQueueService _inbound;

    public InboundQueueService([FromKeyedServices("inbound")] IQueueService inbound)
    {
        _inbound = inbound;
    }

    public Task SendAsync(InboundMessage message, CancellationToken ct = default) =>
        _inbound.SendMessageAsync(JsonSerializer.Serialize(message), ct);
}