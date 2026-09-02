namespace WeakAppHandler.ServiceDefaults.Auth;

/// <summary>
/// Authorization policy names shared by every service host. Seed users carry role claims of
/// "Viewer" and "Admin" (the admin policy is a strict subset of the viewer policy); seed machine
/// clients instead carry a single space-separated "scope" claim, which <see cref="IngestionAdmin"/>
/// checks membership of rather than requiring a role.
/// </summary>
public static class ServicePolicies
{
    public const string Viewer = "ViewerPolicy";

    public const string Admin = "AdminPolicy";

    /// <summary>
    /// Guards the Ingestor's admin REST surface (TASK-017) and the Processor's
    /// <c>/api/v1/processing/stats</c> (TASK-021), which only the Gateway's machine client — not a
    /// browser user — calls. Requires the <c>ingestion:admin</c> scope rather than a role, matching
    /// how the Auth Service's client-credentials grant issues machine tokens. The one seeded machine
    /// client is shared by both admin surfaces, so a second, identically-checked policy name would
    /// add nothing; a dedicated <c>processing:admin</c> scope can be split out later if the two
    /// surfaces ever need different grantees.
    /// </summary>
    public const string IngestionAdmin = "IngestionAdminPolicy";

    /// <summary>
    /// The scope <see cref="IngestionAdmin"/> requires. Must stay identical to the Auth Service's
    /// <c>AuthSeedData.ServiceClientScope</c>, which is what the seeded machine client is granted;
    /// the value is duplicated rather than shared because ServiceDefaults is referenced BY the Auth
    /// Service and so cannot reference it back.
    /// </summary>
    public const string IngestionAdminScope = "ingestion:admin";
}
