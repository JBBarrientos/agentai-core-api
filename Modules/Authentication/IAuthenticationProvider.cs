namespace AgentAI.Modules.Authentication;

public interface IAuthenticationProvider
{
    Task<string> SignUpAsync(string email, string password);
    Task<SignInResult> SignInAsync(string email, string password);

    Task ConfirmUserAsync(string email, string code);
    Task ResendConfirmationAsync(string email);

    Task ForgotPasswordAsync(string email);
    Task ConfirmForgotPasswordAsync(string email, string code, string newPassword);

    Task SignOutAsync(string accessToken);

    Task<bool> ValidateTokenAsync(string token);
}