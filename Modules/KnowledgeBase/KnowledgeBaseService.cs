using System.Data;
using System.Text;
using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.KnowledgeBase;

public sealed class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public KnowledgeBaseService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
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

        await using var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "queryFilter", normalizedQuery);
        AddParameter(command, "systemFilter", normalizedSystem);
        AddParameter(command, "queryLike", $"%{normalizedQuery}%");
        AddParameter(command, "systemLike", $"%{normalizedSystem}%");
        AddParameter(command, "limit", limit);

        var results = new List<KnowledgeBaseSearchResult>();
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var score = reader.GetDecimal("score");
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
                score >= 8 ? "alta" : score >= 4 ? "media" : "baja"));
        }

        return results;
    }

    private string GetSchemaPrefix()
    {
        var database = _configuration["KnowledgeBase:DatabaseName"] ?? "kb_support_ai";
        return string.IsNullOrWhiteSpace(database) ? string.Empty : $"`{database}`.";
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = $"@{name}";
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

