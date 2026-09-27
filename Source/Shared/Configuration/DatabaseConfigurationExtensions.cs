using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace Shared.Configuration;

public static class DatabaseConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public string GetDatabaseConnectionString()
        {
            var connectionString = configuration.GetConnectionString(DatabaseConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"ConnectionStrings:{DatabaseConnectionStringName} is not configured.");

            if (configuration[DatabasePasswordKey] is not { Length: > 0 } password)
            {
                return connectionString;
            }

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            builder["Password"] = password;

            return builder.ConnectionString;
        }
    }

    // Below the extension block because StyleCop does not recognise extension blocks yet and
    // reports any of them that follows a field or a property (SA1201).
    public const string DatabaseConnectionStringName = "Database";

    public const string DatabasePasswordKey = "Database:Password";
}
