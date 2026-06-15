using AgentAI.Modules.Tickets;

namespace AgentAI.Modules.Metrics;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/metricas", async (ITicketRepository repository, CancellationToken ct) =>
        {
            var todos = (await repository.GetAllAsync(ct)).ToList();

            var ingresados  = todos.Count;
            var resueltos   = todos.Count(t => t.State is 4 or 5);      // Resolved, Closed
            var escalados   = todos.Count(IsEscalated);
            var noResueltos = todos.Count(t => !IsEscalated(t) && t.State is 1 or 2 or 3); // New, In Progress, On Hold

            return Results.Ok(new { ingresados, resueltos, noResueltos, escalados });
        })
        .WithTags("Metrics")
        .AllowAnonymous();

        app.MapGet("/metricas/calidad", async (ITicketRepository repository, CancellationToken ct) =>
        {
            var todos = (await repository.GetAllAsync(ct)).ToList();

            var fallasPorModulo = todos
                .GroupBy(t => string.IsNullOrWhiteSpace(t.AffectedSystem) ? "sin clasificar" : t.AffectedSystem)
                .Select(g => new
                {
                    modulo = g.Key,
                    fallas = g.Count()
                })
                .OrderByDescending(g => g.fallas)
                .ToList();

            return Results.Ok(new { fallasPorModulo });
        })
        .WithTags("Metrics")
        .AllowAnonymous();

        return app;
    }

    private static bool IsEscalated(Ticket ticket)
        => ticket.AssignmentGroup.Equals("Soporte Nivel 2", StringComparison.OrdinalIgnoreCase) &&
           ticket.StateLabel.Equals("In Progress", StringComparison.OrdinalIgnoreCase);
}
