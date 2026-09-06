using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulier.Zentrale.Application;

namespace Soulier.Zentrale.Infrastructure;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSoulierPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(connectionString));

        services.AddDbContext<SoulierDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<EfHumanAccessReader>();
        services.AddScoped<IHumanPrincipalRegistry>(provider =>
            provider.GetRequiredService<EfHumanAccessReader>());
        services.AddScoped<IHumanAccessReader>(provider =>
            provider.GetRequiredService<EfHumanAccessReader>());
        services.AddScoped<IHumanAccessAdministration, EfHumanAccessAdministration>();

        services.AddScoped<EfClientAccess>();
        services.AddScoped<IClientAccessReader>(provider =>
            provider.GetRequiredService<EfClientAccess>());
        services.AddScoped<IClientAccessAdministration>(provider =>
            provider.GetRequiredService<EfClientAccess>());

        services.AddScoped<EfServiceAccess>();
        services.AddScoped<IServiceAccessReader>(provider =>
            provider.GetRequiredService<EfServiceAccess>());
        services.AddScoped<IServiceAccessAdministration>(provider =>
            provider.GetRequiredService<EfServiceAccess>());

        return services;
    }
}
