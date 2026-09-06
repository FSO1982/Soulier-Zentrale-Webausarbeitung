namespace Soulier.Zentrale.Api;

public static class SoulierRuntimeConfiguration
{
    public static string? ResolveDatabaseConnectionString(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var connectionString = configuration.GetConnectionString("Soulier");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (environment.IsProduction())
                throw new InvalidOperationException(
                    "PostgreSQL connection string 'ConnectionStrings:Soulier' is mandatory in Production.");

            return null;
        }

        return connectionString;
    }
}
