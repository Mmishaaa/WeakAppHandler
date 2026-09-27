using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace Shared.Configuration;

public static class DatabaseConfigurationExtensions
{
    public const string DatabaseConnectionStringName = "Database";

    public const string DatabasePasswordKey = "Database:Password";

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
}
