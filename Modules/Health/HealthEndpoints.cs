using AgentAI.Data;
using Microsoft.EntityFrameworkCore;
namespace AgentAI.Modules.Health;

public static class HealthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health");

        group.MapGet("/live", () =>
            Results.Ok(new { status = "alive" })
        );

        group.MapGet("/ready", async (AppDbContext db) =>
        {
            var canConnect = await db.Database.CanConnectAsync();
            return canConnect ? Results.Ok(new { status = true }) : Results.Problem("Database unreachable");
        });

    }
}