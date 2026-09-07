using System.ComponentModel.DataAnnotations;

namespace DataIngestorService.Configuration;

// Connection settings for the message broker. Validated on start for the same reason the WeakApp
// options are: a service that cannot reach the broker should fail loudly at boot, not silently
// drop every batch it collects.
sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string VirtualHost { get; set; } = "/";

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
