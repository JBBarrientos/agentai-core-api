using AgentAI.Modules.AgentSteps;
using AgentAI.Modules.Tickets;

namespace AgentAI.Modules.AgentRuns;

public class AgentRun
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public ICollection<AgentStep> AgentSteps { get; set; } = new List<AgentStep>();
}