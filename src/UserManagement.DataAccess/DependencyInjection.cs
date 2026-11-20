using Microsoft.Extensions.DependencyInjection;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;
using UserManagement.DataAccess.Repositories;

namespace UserManagement.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string provider)
    {
        if (string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SqliteDatabase>();
            services.AddScoped<IUserRepository, SqliteUserRepository>();
        }
        else if (string.Equals(provider, "MongoDB", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IUserRepository, UserRepository>();
        }
        else
        {
            throw new InvalidOperationException("Database:Provider must be SQLite or MongoDB.");
        }
        return services;
    }
}
