using AgentAI.Modules.AgentRuns;
using AgentAI.Modules.KbUsages;

namespace AgentAI.Modules.AgentSteps;

public class AgentStep
{
    public int Id { get; set; }
    public int AgentRunId { get; set; }
    public string AgentType { get; set; } = string.Empty;
    public string InputData { get; set; } = string.Empty;
    public string OutputData { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public AgentRun AgentRun { get; set; } = null!;
    public ICollection<KbUsage> KbUsages { get; set; } = new List<KbUsage>();
}