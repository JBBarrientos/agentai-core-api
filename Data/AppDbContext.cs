using Microsoft.EntityFrameworkCore;
using AgentAI.Modules.Users;
using AgentAI.Modules.Tickets;
using AgentAI.Modules.Conversations;
using AgentAI.Modules.Messages;

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

}
