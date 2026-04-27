using AgentAI.Modules.Users;
namespace AgentAI.Modules.Authentication;

public class AuthenticationService
{
    private readonly IAuthenticationProvider _provider;
    private readonly IUserRepository _userRepository;

    public AuthenticationService(IAuthenticationProvider provider, IUserRepository userRepository)
    {
        _provider = provider;
        _userRepository = userRepository;
    }

    public async Task SignUpAsync(string email, string password)
    {
        var userSub = await _provider.SignUpAsync(email, password);
        await _userRepository.CreateAsync(new User
        {
            Name = email,
            ExternalId = userSub
        });
    }

    public Task<SignInResult> SignInAsync(string email, string password)
        => _provider.SignInAsync(email, password);

    public Task ConfirmUserAsync(string email, string code)
        => _provider.ConfirmUserAsync(email, code);

    public Task ResendConfirmationAsync(string email)
        => _provider.ResendConfirmationAsync(email);

    public Task ForgotPasswordAsync(string email)
        => _provider.ForgotPasswordAsync(email);

    public Task ConfirmForgotPasswordAsync(string email, string code, string newPassword)
        => _provider.ConfirmForgotPasswordAsync(email, code, newPassword);

    public Task SignOutAsync(string accessToken)
        => _provider.SignOutAsync(accessToken);

    public Task<bool> ValidateTokenAsync(string token)
        => _provider.ValidateTokenAsync(token);
}