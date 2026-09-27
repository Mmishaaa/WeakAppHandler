using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Shared.Configuration;

public static class ConfigurationBuilderExtensions
{
    public const string ThresholdsFileName = "thresholds.json";

    public const string DockerSecretsDirectory = "/run/secrets";

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
}
