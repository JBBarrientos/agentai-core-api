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
            var noResueltos = todos.Count(t => t.State is 1 or 2 or 3); // New, In Progress, On Hold
            var escalados   = todos.Count(t => t.StateLabel == "In Progress - Escalated");

            return Results.Ok(new { ingresados, resueltos, noResueltos, escalados });
        })
        .WithTags("Metrics")
        .AllowAnonymous();

        app.MapGet("/metricas/calidad", async (ITicketRepository repository, CancellationToken ct) =>
        {
            var todos = (await repository.GetAllAsync(ct)).ToList();

            // Agrupa tickets por módulo detectado del título
            var porModulo = todos
                .GroupBy(t => DetectarModulo(t.Title + " " + t.Description))
                .Select(g => new
                {
                    modulo = g.Key,
                    fallas = g.Count(t => t.StateLabel == "In Progress - Escalated")
                })
                .Where(g => g.fallas > 0)
                .OrderByDescending(g => g.fallas)
                .ToList();

            // Tasa de error por agente: escalados / total del módulo * 100
            var porAgente = todos
                .GroupBy(t => DetectarAgente(t.Title + " " + t.Description))
                .Select(g =>
                {
                    var total     = g.Count();
                    var fallados  = g.Count(t => t.StateLabel == "In Progress - Escalated");
                    var tasa      = total == 0 ? 0 : (int)Math.Round(fallados * 100.0 / total);
                    return new { agente = g.Key, tasa };
                })
                .Where(g => g.tasa > 0)
                .OrderByDescending(g => g.tasa)
                .ToList();

            return Results.Ok(new
            {
                fallasPorModulo = porModulo,
                errorPorAgente  = porAgente
            });
        })
        .WithTags("Metrics")
        .AllowAnonymous();

        return app;
    }

    // Detecta el módulo (nombre amigable) a partir del texto del ticket
    private static string DetectarModulo(string texto)
    {
        var t = texto.ToLowerInvariant();

        if (t.Contains("turno") || t.Contains("turnera") || t.Contains("reserva"))
            return "Turno / Reserva";
        if (t.Contains("acceso") || t.Contains("login") || t.Contains("contrase") ||
            t.Contains("password") || t.Contains("sesion"))
            return "Acceso / Login";
        if (t.Contains("pago") || t.Contains("cobro") || t.Contains("tarjeta") ||
            t.Contains("credito") || t.Contains("debito"))
            return "Pagos";
        if (t.Contains("pedido") || t.Contains("orden") || t.Contains("ord-"))
            return "Pedidos";
        if (t.Contains("catalogo") || t.Contains("precio"))
            return "Catálogo / Precio";
        if (t.Contains("stock") || t.Contains("inventario"))
            return "Stock";

        return "Otros";
    }

    // Detecta el agente responsable a partir del texto del ticket
    private static string DetectarAgente(string texto)
    {
        var t = texto.ToLowerInvariant();

        if (t.Contains("turno") || t.Contains("turnera") || t.Contains("reserva"))
            return "Subag. Turno";
        if (t.Contains("acceso") || t.Contains("login") || t.Contains("contrase") ||
            t.Contains("password") || t.Contains("sesion"))
            return "Subag. Acceso";
        if (t.Contains("pago") || t.Contains("cobro") || t.Contains("tarjeta") ||
            t.Contains("credito") || t.Contains("debito"))
            return "Subag. Pago";
        if (t.Contains("pedido") || t.Contains("orden") || t.Contains("ord-"))
            return "Subag. Pedido";
        if (t.Contains("catalogo") || t.Contains("precio"))
            return "Subag. Precio";
        if (t.Contains("stock") || t.Contains("inventario"))
            return "Subag. Stock";

        return "Agente Entrada";
    }
}
