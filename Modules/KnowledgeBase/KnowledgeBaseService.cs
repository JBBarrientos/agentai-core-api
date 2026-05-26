using MySqlConnector;

namespace AgentAI.Modules.KnowledgeBase;

public sealed class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly IConfiguration _configuration;

    public KnowledgeBaseService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<KnowledgeBaseSearchResult>> SearchAsync(
        string query,
        string? system,
        int limit = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(system))
            return Array.Empty<KnowledgeBaseSearchResult>();

        limit = Math.Clamp(limit, 1, 20);

        var schema = GetSchemaPrefix();
        var sql = $"""
            SELECT
                c.article_id,
                COALESCE(CONCAT('KB-', c.article_id), '') AS article_code,
                COALESCE(GROUP_CONCAT(DISTINCT s.name ORDER BY s.name SEPARATOR ', '), '') AS systems,
                COALESCE(GROUP_CONCAT(DISTINCT s.system_type ORDER BY s.system_type SEPARATOR ', '), '') AS system_types,
                COALESCE(GROUP_CONCAT(DISTINCT t.name ORDER BY t.name SEPARATOR ', '), '') AS tags,
                COALESCE(GROUP_CONCAT(DISTINCT a.name ORDER BY a.name SEPARATOR ', '), '') AS actions,
                COALESCE(c.description, '') AS description,
                COALESCE(c.symptoms, '') AS symptoms,
                COALESCE(c.probable_cause, '') AS probable_cause,
                COALESCE(c.required_data, '') AS required_data,
                COALESCE(c.preconditions, '') AS preconditions,
                COALESCE(c.recommended_action, '') AS recommended_action,
                COALESCE(c.validation, '') AS validation,
                COALESCE(c.expected_result, '') AS expected_result,
                COALESCE(c.escalation_criteria, '') AS escalation_criteria,
                COALESCE(c.suggested_user_message, '') AS suggested_user_message,
                (
                    CASE WHEN @systemFilter = '' THEN 0 ELSE
                        MAX(CASE
                            WHEN LOWER(COALESCE(s.name, '')) = @systemFilter THEN 5
                            WHEN LOWER(COALESCE(s.system_type, '')) = @systemFilter THEN 4
                            WHEN LOWER(COALESCE(s.name, '')) LIKE @systemLike THEN 3
                            ELSE 0
                        END)
                    END
                    +
                    CASE WHEN @queryFilter = '' THEN 0 ELSE
                        CASE WHEN LOWER(COALESCE(c.description, '')) LIKE @queryLike THEN 3 ELSE 0 END +
                        CASE WHEN LOWER(COALESCE(c.symptoms, '')) LIKE @queryLike THEN 3 ELSE 0 END +
                        CASE WHEN LOWER(COALESCE(c.recommended_action, '')) LIKE @queryLike THEN 2 ELSE 0 END +
                        CASE WHEN LOWER(COALESCE(c.probable_cause, '')) LIKE @queryLike THEN 1 ELSE 0 END +
                        CASE WHEN LOWER(COALESCE(c.required_data, '')) LIKE @queryLike THEN 1 ELSE 0 END +
                        CASE WHEN LOWER(COALESCE(t.name, '')) LIKE @queryLike THEN 2 ELSE 0 END
                    END
                ) AS score
            FROM {schema}kb_article_content c
            LEFT JOIN {schema}kb_article_systems ars ON ars.article_id = c.article_id
            LEFT JOIN {schema}kb_systems s ON s.id = ars.system_id
            LEFT JOIN {schema}kb_article_tags art ON art.article_id = c.article_id
            LEFT JOIN {schema}kb_tags t ON t.id = art.tag_id
            LEFT JOIN {schema}kb_article_actions ara ON ara.article_id = c.article_id
            LEFT JOIN {schema}kb_actions a ON a.id = ara.action_id
            WHERE
                (
                    @systemFilter = ''
                    OR LOWER(COALESCE(s.name, '')) LIKE @systemLike
                    OR LOWER(COALESCE(s.system_type, '')) LIKE @systemLike
                    OR LOWER(COALESCE(c.description, '')) LIKE @systemLike
                    OR LOWER(COALESCE(c.symptoms, '')) LIKE @systemLike
                )
                AND
                (
                    @queryFilter = ''
                    OR LOWER(COALESCE(c.description, '')) LIKE @queryLike
                    OR LOWER(COALESCE(c.symptoms, '')) LIKE @queryLike
                    OR LOWER(COALESCE(c.probable_cause, '')) LIKE @queryLike
                    OR LOWER(COALESCE(c.required_data, '')) LIKE @queryLike
                    OR LOWER(COALESCE(c.recommended_action, '')) LIKE @queryLike
                    OR LOWER(COALESCE(c.escalation_criteria, '')) LIKE @queryLike
                    OR LOWER(COALESCE(t.name, '')) LIKE @queryLike
                )
            GROUP BY
                c.article_id,
                c.description,
                c.symptoms,
                c.probable_cause,
                c.required_data,
                c.preconditions,
                c.recommended_action,
                c.validation,
                c.expected_result,
                c.escalation_criteria,
                c.suggested_user_message
            HAVING score > 0
            ORDER BY score DESC, c.article_id ASC
            LIMIT @limit;
            """;

        var normalizedQuery = Normalize(query);
        var normalizedSystem = Normalize(system);

        await using var connection = new MySqlConnection(GetConnectionString());
        await connection.OpenAsync(ct);

        await using var setMode = connection.CreateCommand();
        setMode.CommandText = "SET SESSION sql_mode = ''";
        await setMode.ExecuteNonQueryAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@queryFilter", normalizedQuery);
        command.Parameters.AddWithValue("@systemFilter", normalizedSystem);
        command.Parameters.AddWithValue("@queryLike", $"%{normalizedQuery}%");
        command.Parameters.AddWithValue("@systemLike", $"%{normalizedSystem}%");
        command.Parameters.AddWithValue("@limit", limit);

        var results = new List<KnowledgeBaseSearchResult>();
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var score = Convert.ToDecimal(reader["score"]);
            results.Add(new KnowledgeBaseSearchResult(
                reader.GetInt32("article_id"),
                reader.GetString("article_code"),
                reader.GetString("systems"),
                reader.GetString("system_types"),
                reader.GetString("tags"),
                reader.GetString("actions"),
                reader.GetString("description"),
                reader.GetString("symptoms"),
                reader.GetString("probable_cause"),
                reader.GetString("required_data"),
                reader.GetString("preconditions"),
                reader.GetString("recommended_action"),
                reader.GetString("validation"),
                reader.GetString("expected_result"),
                reader.GetString("escalation_criteria"),
                reader.GetString("suggested_user_message"),
                score >= 3 ? "alta" : score >= 2 ? "media" : "baja"));
        }

        return results;
    }

    public async Task<DiagnosticarResponse> DiagnosticarAsync(
        string sistema,
        string descripcion,
        CancellationToken ct = default)
    {
        var keyword = ExtractKeyword(descripcion);
        var results = await SearchAsync(keyword, system: null, limit: 3, ct);

        if (results.Count == 0)
            return new DiagnosticarResponse(
                PuedoResolver: false,
                Decision: "escalar",
                MensajeSugerido: "No encontré información en la base de conocimiento para resolver este caso. Lo escalo a soporte de nivel 2.",
                CriteriosEscalacion: string.Empty,
                AccionesRecomendadas: string.Empty,
                Confianza: "ninguna",
                ArticleId: null,
                ArticleCode: null);

        var top = results[0];
        var decision = ResolveDecision(top);

        return new DiagnosticarResponse(
            PuedoResolver: decision == "continuar",
            Decision: decision,
            MensajeSugerido: top.SuggestedUserMessage,
            CriteriosEscalacion: top.EscalationCriteria,
            AccionesRecomendadas: top.RecommendedAction,
            Confianza: top.Confidence,
            ArticleId: top.ArticleId,
            ArticleCode: top.ArticleCode);
    }

    private static string ResolveDecision(KnowledgeBaseSearchResult article)
    {
        var escalationKeywords = new[] { "escalar", "nivel 2", "especialista", "soporte avanzado" };
        var hasEscalationCriteria = escalationKeywords.Any(k =>
            article.EscalationCriteria.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (hasEscalationCriteria && article.Confidence != "alta")
            return "escalar";

        if (article.Confidence == "baja")
            return "escalar";

        if (article.Confidence == "media" && !string.IsNullOrWhiteSpace(article.RequiredData))
            return "pedir_mas_info";

        return "continuar";
    }

    private static string ExtractKeyword(string description)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "no", "me", "la", "el", "en", "de", "mi", "se", "lo", "que", "un", "una",
            "es", "al", "del", "con", "por", "para", "como", "pero", "si", "ya", "su",
            "le", "yo", "tu", "y", "a", "o", "fue", "han", "hay", "hace", "cada",
            "esto", "esta", "este", "puedo", "pero", "figura", "tengo"
        };

        var keyword = description
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', ';', ':', '!', '?'))
            .FirstOrDefault(w => w.Length > 5 && !stopWords.Contains(w));

        return keyword ?? description[..Math.Min(30, description.Length)];
    }

    private string GetConnectionString()
        => _configuration["KnowledgeBase:ConnectionString"]
            ?? throw new InvalidOperationException("KnowledgeBase:ConnectionString no está configurado.");

    private string GetSchemaPrefix()
    {
        var database = _configuration["KnowledgeBase:DatabaseName"] ?? "kb_support_ai";
        return string.IsNullOrWhiteSpace(database) ? string.Empty : $"`{database}`.";
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
