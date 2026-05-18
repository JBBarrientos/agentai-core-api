namespace AgentAI.Modules.Tickets.Dto;

public record CreateAgentTicketRequest(
    string Description,
    string System,
    string ErrorType,
    string UserEmail
);
