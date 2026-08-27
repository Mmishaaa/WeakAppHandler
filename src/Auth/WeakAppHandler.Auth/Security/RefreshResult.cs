using WeakAppHandler.Auth.Domain;

namespace WeakAppHandler.Auth.Security;

public sealed record RefreshResult(User User, string RawToken);
