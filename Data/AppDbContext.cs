using Microsoft.EntityFrameworkCore;
using AgentAI.Modules.Users;
using AgentAI.Modules.Tickets;
using AgentAI.Modules.Conversations;
using AgentAI.Modules.Messages;
using AgentAI.Modules.AuditLog;
using AgentAI.Modules.AgentRuns;
using AgentAI.Modules.AgentSteps;
using AgentAI.Modules.KbUsages;

namespace AgentAI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AuditLog> AuditLog => Set<AuditLog>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<KbUsage> KbUsages => Set<KbUsage>();

    

}
