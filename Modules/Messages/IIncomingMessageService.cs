using AgentAI.Modules.Messages.Dto;

namespace AgentAI.Modules.Messages;

public interface IIncomingMessageService
{
    Task<IncomingMessageResponse> ProcessIncomingAsync(IncomingMessageRequest req, CancellationToken ct = default);

    Task<IncomingMessageResponse> ProcessOutboundAsync(IncomingMessageRequest req, CancellationToken ct = default);
}