using Microsoft.EntityFrameworkCore;
using AgentAI.Modules.Health;
using AgentAI.Data;
using AgentAI.Extensions;
using AgentAI.Modules.Tickets;
using AgentAI.Modules.Messages;
using AgentAI.Modules.Conversations;
using AgentAI.Modules.Authentication;
using AgentAI.Modules.Authentication.Cognito;
using Amazon.CognitoIdentityProvider;
using AgentAI.Modules.Queue;

var builder = WebApplication.CreateBuilder(args);

// This loads secrets in Development automatically — but only if your project has a UserSecretsId
// Make sure this is present:
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddUserSecrets<Program>(optional: true)  // 👈 Add this explicitly
    .AddEnvironmentVariables();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthModule();
builder.Services.AddTicketModule();
builder.Services.AddConversationModule();
builder.Services.AddMessageModule();
builder.Services.AddAuthenticationModule(builder.Configuration);
builder.Services.AddCognitoAuthentication(builder.Configuration);

builder.Services.Configure<CognitoOptions>(
    builder.Configuration.GetSection("Cognito"));

builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<IAuthenticationProvider, CognitoAuthenticationProvider>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddQueueModule(builder.Configuration);
}

var app = builder.Build();
app.UseGlobalExceptionHandler();
app.UseCors("AllowAll");
app.UseAuthentication(); 
app.UseAuthorization();
app.MapHealthModule();
app.MapTicketModule();
app.MapConversationModule();
app.MapMessageModule();
app.MapAuthenticationModule();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi()
.RequireAuthorization();

app.MapPost("/dev/queue/send", async (InboundQueueService queue) =>
{
    await queue.SendAsync(new InboundMessage(
        TicketId: "INC0001",
        CorrelationId: Guid.NewGuid().ToString(),
        CustomerId: "juan.perez@empresa.com",
        Metadata: []
    ));

    return Results.Ok("Message sent to inbound queue");
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
