using AgentAI.Modules.AgentSteps;

namespace AgentAI.Modules.KbUsages;

public class KbUsage
{
    public int Id { get; set; }
    public int AgentStepId { get; set; }
    public string ExternalArticleId { get; set; } = string.Empty;
    public string ArticleTitle { get; set; } = string.Empty;
    public bool? ResultedInResolution { get; set; }
    public DateTime UsedAt { get; set; }
    public AgentStep AgentStep { get; set; } = null!;
}