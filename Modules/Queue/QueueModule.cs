using Amazon.SQS;

namespace AgentAI.Modules.Queue;
public static class QueueModule
{
    public static IServiceCollection AddQueueModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddAWSService<IAmazonSQS>();

        var inboundUrl = config["SQS:InboundQueueUrl"]!;
        var outboundUrl = config["SQS:OutboundQueueUrl"]!;

        services.AddKeyedSingleton<IQueueService>("inbound", (sp, _) =>
            new SqsMessageQueue(sp.GetRequiredService<IAmazonSQS>(), inboundUrl));

        services.AddKeyedSingleton<IQueueService>("outbound", (sp, _) =>
            new SqsMessageQueue(sp.GetRequiredService<IAmazonSQS>(), outboundUrl));

        services.AddSingleton<ActionDispatcher>();
        services.AddHostedService<OutboundQueueWorker>();
        services.AddScoped<InboundQueueService>();

        return services;
    }
}