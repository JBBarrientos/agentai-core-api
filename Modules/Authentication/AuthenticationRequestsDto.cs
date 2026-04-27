namespace AgentAI.Modules.Authentication.Api;

public record SignUpRequest(string Email, string Password);
public record SignInRequest(string Email, string Password);
public record ConfirmUserRequest(string Email, string Code);
public record ResendConfirmationRequest(string Email);

public record ForgotPasswordRequest(string Email);
public record ConfirmForgotPasswordRequest(string Email, string Code, string NewPassword);

public record SignOutRequest(string AccessToken);