namespace AgentAI.Modules.Users;
public interface IUserRepository
{
	Task CreateAsync(User user);
	Task<User?> GetByExternalIdAsync(string externalId);
}