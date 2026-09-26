namespace NotificationService.API.Configuration;

internal sealed class ClientAppOptions
{
    public const string SectionName = "ClientApp";

    public List<string> AllowedOrigins { get; set; } = [];
}
