namespace AgentAI.Modules.KnowledgeBase;

public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<KnowledgeBaseSearchResult>> SearchAsync(
        string query,
        string? system,
        int limit = 5,
        CancellationToken ct = default);
}

