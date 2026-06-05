using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.Metrics;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/metricas", async (AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            var escalatedAssignmentGroup = GetEscalatedAssignmentGroup(configuration);
            var tickets = await db.Tickets
                .AsNoTracking()
                .Select(ticket => new TicketMetricRow(ticket.State, ticket.StateLabel, ticket.AssignmentGroup))
                .ToListAsync(ct);

            var escalados = tickets.Count(ticket => IsEscalated(ticket, escalatedAssignmentGroup));
            var resueltos = tickets.Count(ticket => ticket.State is 4 or 5 || IsState(ticket.StateLabel, "Resolved") || IsState(ticket.StateLabel, "Closed"));
            var noResueltos = tickets.Count(ticket =>
                !IsEscalated(ticket, escalatedAssignmentGroup) &&
                (ticket.State is 1 or 2 or 3 || IsState(ticket.StateLabel, "New") || IsState(ticket.StateLabel, "In Progress") || IsState(ticket.StateLabel, "On Hold")));

            return Results.Ok(new
            {
                ingresados = tickets.Count,
                resueltos,
                noResueltos,
                escalados
            });
        })
        .AllowAnonymous();

        app.MapGet("/metricas/calidad", async (AppDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            var escalatedAssignmentGroup = GetEscalatedAssignmentGroup(configuration);
            var escalationCountsAsAgentFailure = configuration.GetValue("Metrics:EscalationCountsAsAgentFailure", true);
            var escalationOwnerAgent = configuration["Metrics:EscalationOwnerAgent"] ?? string.Empty;

            var ticketsBySystem = await db.Tickets
                .AsNoTracking()
                .GroupBy(ticket => string.IsNullOrWhiteSpace(ticket.AffectedSystem) ? "sin clasificar" : ticket.AffectedSystem)
                .Select(group => new
                {
                    modulo = group.Key,
                    fallas = group.Count()
                })
                .OrderByDescending(item => item.fallas)
                .ToListAsync(ct);

            var failedSteps = await db.AgentSteps
                .AsNoTracking()
                .Where(step => step.Status.ToLower() == "failed" || step.Status.ToLower() == "error")
                .GroupBy(step => step.AgentType)
                .Select(group => new
                {
                    modulo = string.IsNullOrWhiteSpace(group.Key) ? "Sin agente" : group.Key,
                    fallas = group.Count()
                })
                .OrderByDescending(item => item.fallas)
                .ToListAsync(ct);

            var canAttributeEscalations = escalationCountsAsAgentFailure &&
                !string.IsNullOrWhiteSpace(escalatedAssignmentGroup) &&
                !string.IsNullOrWhiteSpace(escalationOwnerAgent);

            var escalatedTickets = canAttributeEscalations
                ? await db.Tickets
                    .AsNoTracking()
                    .CountAsync(ticket => ticket.AssignmentGroup == escalatedAssignmentGroup, ct)
                : 0;

            var escalatedTicketsWithEntradaStep = canAttributeEscalations
                ? await db.AgentSteps
                    .AsNoTracking()
                    .Where(step =>
                        step.AgentType == escalationOwnerAgent &&
                        step.AgentRun.Ticket.AssignmentGroup == escalatedAssignmentGroup)
                    .Select(step => step.AgentRun.TicketId)
                    .Distinct()
                    .CountAsync(ct)
                : 0;

            var totalStepsByAgent = await db.AgentSteps
                .AsNoTracking()
                .GroupBy(step => step.AgentType)
                .Select(group => new
                {
                    AgentType = group.Key,
                    Total = group.Count()
                })
                .ToListAsync(ct);

            var failedByAgent = failedSteps.ToDictionary(item => item.modulo, item => item.fallas, StringComparer.OrdinalIgnoreCase);
            if (canAttributeEscalations && escalatedTickets > 0)
                failedByAgent[escalationOwnerAgent] = failedByAgent.GetValueOrDefault(escalationOwnerAgent) + escalatedTickets;

            var totalByAgent = totalStepsByAgent.ToDictionary(
                item => string.IsNullOrWhiteSpace(item.AgentType) ? "Sin agente" : item.AgentType,
                item => item.Total,
                StringComparer.OrdinalIgnoreCase);

            var escalatedWithoutEntradaStep = Math.Max(escalatedTickets - escalatedTicketsWithEntradaStep, 0);
            if (canAttributeEscalations && escalatedWithoutEntradaStep > 0)
                totalByAgent[escalationOwnerAgent] = totalByAgent.GetValueOrDefault(escalationOwnerAgent) + escalatedWithoutEntradaStep;

            var errorPorAgente = totalStepsByAgent
                .Select(item => string.IsNullOrWhiteSpace(item.AgentType) ? "Sin agente" : item.AgentType)
                .Concat(failedByAgent.Keys)
                .Concat(totalByAgent.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(agent =>
                {
                    failedByAgent.TryGetValue(agent, out var failures);
                    totalByAgent.TryGetValue(agent, out var total);
                    return new
                    {
                        agente = agent,
                        total,
                        fallas = failures,
                        exitos = Math.Max(total - failures, 0),
                        tasa = total == 0 ? 0 : Math.Round((double)failures / total * 100, 2)
                    };
                })
                .OrderByDescending(item => item.tasa)
                .ThenByDescending(item => item.total)
                .ToList();

            return Results.Ok(new
            {
                fallasPorModulo = ticketsBySystem,
                fallasPorAgente = failedSteps,
                errorPorAgente
            });
        })
        .AllowAnonymous();

        return app;
    }

    private static bool IsEscalated(TicketMetricRow ticket, string escalatedAssignmentGroup)
        => IsAssignmentGroup(ticket.AssignmentGroup, escalatedAssignmentGroup);

    private static string GetEscalatedAssignmentGroup(IConfiguration configuration)
        => configuration["Metrics:EscalatedAssignmentGroup"]
            ?? configuration["ServiceNow:EscalationAssignmentGroupName"]
            ?? string.Empty;

    private static bool IsState(string? actual, string expected)
        => actual?.Equals(expected, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsAssignmentGroup(string? actual, string expected)
        => actual?.Equals(expected, StringComparison.OrdinalIgnoreCase) == true;

    private sealed record TicketMetricRow(int State, string StateLabel, string? AssignmentGroup);
}
