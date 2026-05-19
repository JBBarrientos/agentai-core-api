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
using Microsoft.AspNetCore.Mvc;
using AgentAI.Modules.AuditLog;
using AgentAI.Modules.AgentRuns;
using AgentAI.Modules.AgentSteps;
using AgentAI.Modules.KbUsages;
using AgentAI.Modules.Notifications;
using AgentAI.Modules.KnowledgeBase;

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
builder.Services.AddAuditLogModule();
builder.Services.AddAgentRunModule();
builder.Services.AddMessageModule();
builder.Services.AddAgentStepModule();
builder.Services.AddKbUsageModule();
builder.Services.AddNotificationModule();
builder.Services.AddKnowledgeBaseModule();
builder.Services.AddAuthenticationModule(builder.Configuration);
builder.Services.AddCognitoAuthentication(builder.Configuration);

builder.Services.Configure<CognitoOptions>(
    builder.Configuration.GetSection("Cognito"));

builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<IAuthenticationProvider, CognitoAuthenticationProvider>();
builder.Services.AddQueueModule(builder.Configuration, builder.Environment);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseGlobalExceptionHandler();
app.UseCors("AllowAll");
app.UseAuthentication(); 
app.UseAuthorization();
app.MapHealthModule();
app.MapNotificationModule();
app.MapKnowledgeBaseModule();
app.MapAuthenticationModule();
app.MapTicketModule();
app.MapConversationModule();
app.MapMessageModule();
app.MapAuthenticationModule();
app.MapAuditLogModule();
app.MapAgentRunModule();
app.MapAgentStepModule();
app.MapKbUsageModule();

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


if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
