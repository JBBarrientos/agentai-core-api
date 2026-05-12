namespace AgentAI.Modules.AuditLog;
public class AuditLog
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}