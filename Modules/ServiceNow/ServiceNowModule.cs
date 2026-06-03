namespace AgentAI.Modules.ServiceNow;

public static class ServiceNowModule
{
    public static IServiceCollection AddServiceNowModule(this IServiceCollection services)
    {
        services.AddTransient<ServiceNowRetryHandler>();
        services.AddHttpClient<IServiceNowConnector, ServiceNowConnector>()
            .AddHttpMessageHandler<ServiceNowRetryHandler>();

        return services;
    }
}
