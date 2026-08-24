namespace WeakAppHandler.ServiceDefaults.Auth;

/// <summary>
/// Role-based authorization policy names shared by every service host. Seed users/clients
/// (see the Auth Service) carry role claims of "Viewer" and "Admin"; the admin policy is a
/// strict subset of the viewer policy.
/// </summary>
public static class ServicePolicies
{
    public const string Viewer = "ViewerPolicy";

    public const string Admin = "AdminPolicy";
}
