using Microsoft.EntityFrameworkCore;
using AgentAI.Modules.Users;
namespace AgentAI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<User> Users => Set<User>();
}
