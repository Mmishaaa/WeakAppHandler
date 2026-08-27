namespace WeakAppHandler.Auth.Api;

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresInSeconds, string Role, string Email);
