using AgentAI.Modules.AuditLog.Dto;

namespace AgentAI.Modules.AuditLog;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/audit-logs").WithTags("AuditLogs");

        group.MapGet("/entity/{entityType}/{entityId:int}", async (
            string entityType,
            int entityId,
            IAuditLogService service,
            CancellationToken ct) =>
            Results.Ok(await service.GetByEntityAsync(entityType, entityId, ct)));

        group.MapGet("/{id:int}", async (int id, IAuditLogService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } log
                ? Results.Ok(log)
                : Results.NotFound());

        group.MapPost("/", async (CreateAuditLogRequest req, IAuditLogService service, CancellationToken ct) =>
        {
            await service.CreateAsync(req, ct);
            return Results.Created();
        });

        group.MapDelete("/{id:int}", async (int id, IAuditLogService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}