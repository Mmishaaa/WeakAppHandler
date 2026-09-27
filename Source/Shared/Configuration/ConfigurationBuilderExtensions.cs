using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Shared.Configuration;

public static class ConfigurationBuilderExtensions
{
    extension(IConfigurationBuilder configuration)
    {
        public IConfigurationBuilder AddSharedThresholds()
        {
            var source = new JsonConfigurationSource
            {
                Path = Path.Combine(AppContext.BaseDirectory, ThresholdsFileName),
                Optional = false,
                ReloadOnChange = true,
            };

            source.ResolveFileProvider();
            configuration.Sources.Insert(0, source);

            return configuration;
        }

        public IConfigurationBuilder AddDockerSecrets() =>
            configuration.AddKeyPerFile(DockerSecretsDirectory, optional: true);
    }

    // Below the extension block because StyleCop does not recognise extension blocks yet and
    // reports any of them that follows a field or a property (SA1201).
    public const string ThresholdsFileName = "thresholds.json";

    public const string DockerSecretsDirectory = "/run/secrets";
}
