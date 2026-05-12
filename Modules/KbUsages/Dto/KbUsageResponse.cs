namespace AgentAI.Modules.KbUsages.Dto;

public record KbUsageResponse(
    int Id,
    int AgentStepId,
    string ExternalArticleId,
    string ArticleTitle,
    bool? ResultedInResolution,
    DateTime UsedAt
);