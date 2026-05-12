namespace AgentAI.Modules.KbUsages.Dto;

public record CreateKbUsageRequest(
    int AgentStepId,
    string ExternalArticleId,
    string ArticleTitle
);

