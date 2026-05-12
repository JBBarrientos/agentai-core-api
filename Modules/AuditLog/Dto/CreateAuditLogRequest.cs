namespace AgentAI.Modules.AuditLog.Dto;
public record CreateAuditLogRequest(
    string EntityType,
    int EntityId,
    string Action,
    string Payload
);