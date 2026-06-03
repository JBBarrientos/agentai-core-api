namespace AgentAI.Modules.KnowledgeBase;

public sealed record KnowledgeBaseSearchResult(
    int ArticleId,
    string ArticleCode,
    string System,
    string SystemType,
    string Tags,
    string Actions,
    string Description,
    string Symptoms,
    string ProbableCause,
    string RequiredData,
    string Preconditions,
    string RecommendedAction,
    string Validation,
    string ExpectedResult,
    string EscalationCriteria,
    string SuggestedUserMessage,
    string Confidence
);

public sealed record DiagnosticarRequest(string Sistema, string Descripcion);

public sealed record DiagnosticarResponse(
    bool PuedoResolver,
    string Decision,
    string MensajeSugerido,
    string CriteriosEscalacion,
    string AccionesRecomendadas,
    string Confianza,
    int? ArticleId,
    string? ArticleCode
);

