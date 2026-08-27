namespace WeakAppHandler.Auth.Api;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds, string Scope);
