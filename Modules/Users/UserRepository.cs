using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.Users;

public class UserRepository : IUserRepository
{
	private readonly AppDbContext _db;
	public UserRepository(AppDbContext db) => _db = db;

	public async Task CreateAsync(User user)
	{
		_db.Users.Add(user);
		await _db.SaveChangesAsync();
	}

	public async Task<User?> GetByExternalIdAsync(string externalId)
		=> await _db.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId);
}