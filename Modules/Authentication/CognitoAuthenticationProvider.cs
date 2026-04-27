using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.Extensions.Options;
using AgentAI.Modules.Authentication;
using AgentAI.Modules.Authentication.Cognito;

namespace AgentAI.Modules.Authentication.Cognito;

public class CognitoAuthenticationProvider : IAuthenticationProvider
{
    private readonly IAmazonCognitoIdentityProvider _client;
    private readonly CognitoOptions _options;

    public CognitoAuthenticationProvider(
        IAmazonCognitoIdentityProvider client,
        IOptions<CognitoOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<string> SignUpAsync(string email, string password)
    {
        var response = await _client.SignUpAsync(new SignUpRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            Password = password,
            UserAttributes =
            [
                new AttributeType { Name = "email", Value = email }
            ]
        });

        return response.UserSub;
    }

    public async Task<SignInResult> SignInAsync(string email, string password)
    {
        var response = await _client.InitiateAuthAsync(new InitiateAuthRequest
        {
            ClientId = _options.ClientId,
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
        {
            { "USERNAME", email },
            { "PASSWORD", password }
        }
        });

        return new SignInResult(
            response.AuthenticationResult.AccessToken,
            response.AuthenticationResult.RefreshToken
        );
    }

    public async Task ConfirmUserAsync(string email, string code)
    {
        await _client.ConfirmSignUpAsync(new ConfirmSignUpRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            ConfirmationCode = code
        });
    }

    public async Task ResendConfirmationAsync(string email)
    {
        await _client.ResendConfirmationCodeAsync(new ResendConfirmationCodeRequest
        {
            ClientId = _options.ClientId,
            Username = email
        });
    }

    public async Task ForgotPasswordAsync(string email)
    {
        await _client.ForgotPasswordAsync(new ForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email
        });
    }

    public async Task ConfirmForgotPasswordAsync(string email, string code, string newPassword)
    {
        await _client.ConfirmForgotPasswordAsync(new ConfirmForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            ConfirmationCode = code,
            Password = newPassword
        });
    }

    public async Task SignOutAsync(string accessToken)
    {
        await _client.GlobalSignOutAsync(new GlobalSignOutRequest
        {
            AccessToken = accessToken
        });
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        // We are NOT calling Cognito here (already handled by middleware ideally)
        return Task.FromResult(true);
    }
}