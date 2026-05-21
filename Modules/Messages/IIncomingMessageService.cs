using AgentAI.Modules.Messages.Dto;

namespace AgentAI.Modules.Messages;

public interface IIncomingMessageService
{
    Task<IncomingMessageResponse> ProcessIncomingAsync(IncomingMessageRequest req, CancellationToken ct = default);

    Task ProcessOutboundAsync(OutboundMessagePayload payload, CancellationToken ct = default);
}