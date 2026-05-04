namespace AgentAI.Modules.Tickets
{
    public static class TicketModule
    {
        public static IServiceCollection AddTicketModule(this IServiceCollection services)
        {
            services.AddScoped<ITicketRepository, TicketRepository>();
            services.AddScoped<ITicketService, TicketService>();

            return services;
        }

        public static IEndpointRouteBuilder MapTicketModule(this IEndpointRouteBuilder app)
        {
            app.MapTicketEndpoints();
            return app;
        }
    }
}
