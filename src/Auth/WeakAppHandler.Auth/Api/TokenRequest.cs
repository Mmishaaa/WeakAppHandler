namespace WeakAppHandler.Auth.Api;

public sealed record TokenRequest(string ClientId, string ClientSecret);
