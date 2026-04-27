namespace AgentAI.Modules.Authentication.Cognito;

public class CognitoOptions
{
    public string Region { get; set; } = string.Empty;
    public string UserPoolId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
}